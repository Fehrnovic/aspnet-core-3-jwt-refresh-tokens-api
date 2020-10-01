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
    public interface IAuthService
    {
        Task<AuthenticateResponse> Login(AuthenticateRequest model);
        Task<AuthenticateResponse> RefreshToken(string token);
        Task Logout(string token);
    }
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;

        private DataContext _context;
        private IRefreshTokenService _refreshTokenService;

        public AuthService(IConfiguration configuration, DataContext context, IRefreshTokenService refreshTokenService)
        {
            _configuration = configuration;
            _context = context;
            _refreshTokenService = refreshTokenService;
        }

        public async Task<AuthenticateResponse> Login(AuthenticateRequest model)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == model.Username && u.Password == model.Password);

            if (user == null) return null;

            // authentication successful so generate jwt and refresh tokens
            var refreshToken = await _refreshTokenService.CreateRefreshToken(user);

            if (user.RefreshTokens == null)
            {
                user.RefreshTokens = new List<RefreshToken> { refreshToken };
            }
            else
            {
                user.RefreshTokens.Add(refreshToken);
            }

            _context.Update(user);
            _context.SaveChanges();

            var jwtToken = generateJwtToken(refreshToken);

            return new AuthenticateResponse(user, jwtToken, refreshToken.Token);
        }

        public async Task Logout(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var decodedToken = handler.ReadToken(token) as JwtSecurityToken;

            var refreshTokenId = int.Parse(decodedToken.Claims.First(claim => claim.Type == "refreshTokenId").Value);

            var refreshToken = await _context.RefreshTokens.SingleOrDefaultAsync(r => r.Id == refreshTokenId);
            _context.RefreshTokens.Remove(refreshToken);
            await _context.SaveChangesAsync();
        }

        public async Task<AuthenticateResponse> RefreshToken(string token)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

            // return null if no user found with token
            if (user == null) return null;

            var refreshToken = user.RefreshTokens.Single(x => x.Token == token);

            // replace old refresh token with a new one and save
            var newRefreshToken = await _refreshTokenService.CreateRefreshToken(user);
            user.RefreshTokens.Add(newRefreshToken);
            _context.Update(user);
            _context.SaveChanges();

            // generate new jwt
            var jwtToken = generateJwtToken(newRefreshToken);

            return new AuthenticateResponse(user, jwtToken, newRefreshToken.Token);
        }

        // helper methods

        private string generateJwtToken(RefreshToken refreshToken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var test = _configuration["JwtSecret"];
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSecret"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, refreshToken.User.Id.ToString()),
                    new Claim(ClaimTypes.Role, refreshToken.User.Role),
                    new Claim("refreshTokenId", refreshToken.Id.ToString())
                }),
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
