using DocumentManagement.API.DTOs;
using DocumentManagement.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DocumentManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        // ============================================================
        // PUBLIC REGISTRATION
        // ============================================================

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingUser =
                await _userManager.FindByEmailAsync(request.Email);

            if (existingUser != null)
            {
                return BadRequest(new
                {
                    message =
                        "A user with this email already exists."
                });
            }

            // ========================================================
            // PUBLIC REGISTRATION ALWAYS CREATES VIEWER
            // ========================================================

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                EmailConfirmed = true
            };

            var result =
                await _userManager.CreateAsync(
                    user,
                    request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    errors =
                        result.Errors
                            .Select(e => e.Description)
                });
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    user,
                    "Viewer");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                return BadRequest(new
                {
                    errors =
                        roleResult.Errors
                            .Select(e => e.Description)
                });
            }

            return Ok(new
            {
                message =
                    "User registered successfully as Viewer."
            });
        }

        // ============================================================
        // ADMIN CREATE USER
        // ============================================================

        [Authorize(Roles = "Admin")]
        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser(
            RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingUser =
                await _userManager.FindByEmailAsync(
                    request.Email);

            if (existingUser != null)
            {
                return BadRequest(new
                {
                    message =
                        "A user with this email already exists."
                });
            }

            var allowedRoles = new[]
            {
                "Admin",
                "Reviewer",
                "Manager",
                "Finance",
                "Viewer"
            };

            if (!allowedRoles.Contains(request.Role))
            {
                return BadRequest(new
                {
                    message =
                        "Invalid role."
                });
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                EmailConfirmed = true
            };

            var result =
                await _userManager.CreateAsync(
                    user,
                    request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    errors =
                        result.Errors
                            .Select(e => e.Description)
                });
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    user,
                    request.Role);

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                return BadRequest(new
                {
                    errors =
                        roleResult.Errors
                            .Select(e => e.Description)
                });
            }

            return Ok(new
            {
                message =
                    "User created successfully.",

                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    Role = request.Role
                }
            });
        }

        // ============================================================
        // LOGIN
        // ============================================================

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user =
                await _userManager.FindByEmailAsync(
                    request.Email);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Invalid email or password."
                });
            }

            var passwordValid =
                await _userManager.CheckPasswordAsync(
                    user,
                    request.Password);

            if (!passwordValid)
            {
                return Unauthorized(new
                {
                    message =
                        "Invalid email or password."
                });
            }

            var roles =
                await _userManager.GetRolesAsync(user);

            var role =
                roles.FirstOrDefault() ?? "Viewer";

            var token =
                GenerateJwtToken(
                    user,
                    role);

            return Ok(new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = role
            });
        }

        // ============================================================
        // GENERATE JWT
        // ============================================================

        private string GenerateJwtToken(
            ApplicationUser user,
            string role)
        {
            var jwtKey =
                _configuration["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "JWT key is missing from configuration.");
            }

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id),

                new Claim(
                    ClaimTypes.Name,
                    user.UserName ?? string.Empty),

                new Claim(
                    ClaimTypes.Email,
                    user.Email ?? string.Empty),

                new Claim(
                    ClaimTypes.Role,
                    role)
            };

            var key =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey));

            var credentials =
                new SigningCredentials(
                    key,
                    SecurityAlgorithms.HmacSha256);

            var token =
                new JwtSecurityToken(
                    issuer:
                        _configuration["Jwt:Issuer"],

                    audience:
                        _configuration["Jwt:Audience"],

                    claims:
                        claims,

                    expires:
                        DateTime.UtcNow.AddHours(2),

                    signingCredentials:
                        credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}