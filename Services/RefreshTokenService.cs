using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WebApi.Entities;
using WebApi.Helpers;
using WebApi.Models;

namespace WebApi.Services
{
    public interface IRefreshTokenService
    {
        Task<RefreshToken> CreateRefreshToken(User user);
    }
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IConfiguration _configuration;

        private DataContext _context;

        public RefreshTokenService(IConfiguration configuration, DataContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public async Task<RefreshToken> CreateRefreshToken(User user)
        {
            var refreshTokens = await _context.RefreshTokens.Where(r => r.User.Id == user.Id).ToListAsync();

            if (refreshTokens.Count() > 4)
            {
                _context.RefreshTokens.Remove(refreshTokens.FirstOrDefault());
                await _context.SaveChangesAsync();
            }

            using (var rngCryptoServiceProvider = new RNGCryptoServiceProvider())
            {
                var randomBytes = new byte[64];
                rngCryptoServiceProvider.GetBytes(randomBytes);
                return new RefreshToken
                {
                    User = user,
                    Token = Convert.ToBase64String(randomBytes),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Created = DateTime.UtcNow
                };
            }
        }
    }
}
