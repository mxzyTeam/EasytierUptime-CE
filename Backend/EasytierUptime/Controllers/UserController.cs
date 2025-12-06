using EasytierUptime.Data;
using EasytierUptime.DTOs;
using EasytierUptime_Entities.Entities;
using EasytierUptime.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace EasytierUptime.Controllers;

[ApiController]
[Route("api/users")] 
public class UserController : ControllerBase
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IEnumerable<UserDto>> List()
    {
        var users = await FreeSqlDb.Orm.Select<AppUser>().OrderBy(a => a.Id).ToListAsync();
        return users.Select(u => new UserDto(u.Id, u.Username, u.Role, u.CreatedAt, u.Email));
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "username/password required" });
        if (!string.IsNullOrWhiteSpace(req.Email) && !EmailRegex.IsMatch(req.Email))
            return BadRequest(new { message = "invalid email" });
        var exists = await FreeSqlDb.Orm.Select<AppUser>().Where(x => x.Username == req.Username).AnyAsync();
        if (exists) return BadRequest(new { message = "username exists" });
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var emailExists = await FreeSqlDb.Orm.Select<AppUser>().Where(x => x.Email == req.Email).AnyAsync();
            if (emailExists) return BadRequest(new { message = "email exists" });
        }
        var user = new AppUser 
        { 
            Username = req.Username, 
            PasswordHash = PasswordHasher.Hash(req.Password), 
            Role = string.IsNullOrWhiteSpace(req.Role) ? "user" : req.Role,
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email,
            EmailVerified = false
        };
        await FreeSqlDb.Orm.Insert(user).ExecuteAffrowsAsync();
        return Ok(new { id = user.Id });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id <= 0) return BadRequest();
        await FreeSqlDb.Orm.Delete<AppUser>(new { Id = id }).ExecuteAffrowsAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/role")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Role)) return BadRequest();
        await FreeSqlDb.Orm.Update<AppUser>().Set(a => a.Role, req.Role).Where(a => a.Id == id).ExecuteAffrowsAsync();
        return NoContent();
    }

    // Self-service: change own password
    [HttpPut("me/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var name = User.Identity?.Name;
        if (string.IsNullOrEmpty(name)) return Unauthorized();
        var user = await FreeSqlDb.Orm.Select<AppUser>().Where(a => a.Username == name).FirstAsync();
        if (user is null) return Unauthorized();
        if (!PasswordHasher.Verify(req.CurrentPassword ?? string.Empty, user.PasswordHash)) return BadRequest(new { message = "current password invalid" });
        user.PasswordHash = PasswordHasher.Hash(req.NewPassword ?? string.Empty);
        await FreeSqlDb.Orm.Update<AppUser>().SetSource(user).ExecuteAffrowsAsync();
        return NoContent();
    }

    // Admin set password
    [HttpPut("{id:int}/password")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminSetPassword(int id, [FromBody] AdminSetPasswordRequest req)
    {
        var user = await FreeSqlDb.Orm.Select<AppUser>().Where(a => a.Id == id).FirstAsync();
        if (user is null) return NotFound();
        user.PasswordHash = PasswordHasher.Hash(req.Password ?? string.Empty);
        await FreeSqlDb.Orm.Update<AppUser>().SetSource(user).ExecuteAffrowsAsync();
        return NoContent();
    }
}
