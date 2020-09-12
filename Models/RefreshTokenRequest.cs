using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Models
{
    public class RefreshTokenRequest
    {
        [Required]
        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }
    }
}
