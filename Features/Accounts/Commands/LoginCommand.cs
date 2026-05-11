using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OnlineExam.Domain;
using OnlineExam.Domain.Entities;
using OnlineExam.Features.Accounts.Dtos;
using OnlineExam.Shared.Helpers;
using OnlineExam.Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace OnlineExam.Features.Accounts.Commands
{
    public record LoginCommand(LoginReqDTO LoginDTO) : IRequest<ServiceResponse<UserDto>>;

    public class LoginCommandHandler : IRequestHandler<LoginCommand, ServiceResponse<UserDto>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JWT _jwtOptions;

        public LoginCommandHandler(IOptions<JWT> jwtOptions, UserManager<ApplicationUser> userManager)
        {
            _jwtOptions = jwtOptions.Value;
            _userManager = userManager;
        }

        public async Task<ServiceResponse<UserDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            if (request.LoginDTO == null || string.IsNullOrEmpty(request.LoginDTO.Email) || string.IsNullOrEmpty(request.LoginDTO.Password))
            {
                return ServiceResponse<UserDto>.ErrorResponse("Invalid login request", "طلب تسجيل دخول غير صالح", 400);
            }

            var user = await _userManager.FindByEmailAsync(request.LoginDTO.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.LoginDTO.Password))
            {
                return ServiceResponse<UserDto>.UnauthorizedResponse();
            }

            if (!user.EmailConfirmed)
            {
                return ServiceResponse<UserDto>.ErrorResponse(
                    "Email not confirmed. Please check your email and confirm your account.",
                    "البريد الإلكتروني غير مؤكد. يرجى التحقق من بريدك الإلكتروني وتأكيد حسابك.",
                    403);
            }

            // ✅ FIXED: Get actual user roles from database
            var userRoles = await _userManager.GetRolesAsync(user);

            // ✅ FIXED: Create claims list with actual roles
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email),
                new Claim(ClaimTypes.Email, user.Email)
            };

            // ✅ FIXED: Add all user roles as role claims
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secretkey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // Refresh Token Logic
            var refreshToken = GetOrGenerateRefreshToken(user);
            await _userManager.UpdateAsync(user);

            // ✅ FIXED: Use actual roles in UserDto
            var userDto = new UserDto
            {
                IsAuthenticated = true,
                Username = user.UserName ?? user.Email,
                Email = user.Email,
                Roles = userRoles.ToList(), // Actual roles from database
                Token = tokenString,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiration = refreshToken.ExpiresOn,
                EmailConfirmed = user.EmailConfirmed
            };

            return ServiceResponse<UserDto>.SuccessResponse(userDto);
        }

        private RefreshToken GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var generator = RandomNumberGenerator.Create();
            generator.GetBytes(randomNumber);

            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomNumber),
                ExpiresOn = DateTime.UtcNow.AddDays(10),
                CreatedOn = DateTime.UtcNow
            };
        }

        private RefreshToken GetOrGenerateRefreshToken(ApplicationUser user)
        {
            var activeRefreshToken = user.RefreshTokens?.FirstOrDefault(t => t.IsActive);
            if (activeRefreshToken != null)
            {
                return activeRefreshToken;
            }

            user.RefreshTokens ??= new List<RefreshToken>();
            var nextRefreshTokenId = user.RefreshTokens.Count == 0
                ? 1
                : user.RefreshTokens.Max(t => t.Id) + 1;

            var newRefreshToken = GenerateRefreshToken();
            newRefreshToken.Id = nextRefreshTokenId;
            user.RefreshTokens.Add(newRefreshToken);

            return newRefreshToken;
        }
    }
}
