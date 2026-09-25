using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Users;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Users;

public sealed record UserResponse(Guid Id, string Email, string Name, string? Phone, Role Role, bool IsActive, bool HasSignedIn, DateTimeOffset? LastLoginAt)
{
    public static UserResponse From(User user) =>
        new(user.Id, user.Email, user.Name, user.Phone, user.Role, user.IsActive, user.AuthUserId is not null, user.LastLoginAt);
}

public sealed record CreateUserRequest([Required, EmailAddress, MaxLength(320)] string Email, [Required, MaxLength(200)] string Name, Role Role, [MaxLength(40)] string? Phone);

public sealed record UpdateUserRequest([Required, MaxLength(200)] string Name, Role Role, [MaxLength(40)] string? Phone, bool IsActive);

/// <summary>
/// Who has access. Logging in is Supabase's; being let in is a row here. The
/// person signs in with the same e-mail and is bound to their account then.
/// </summary>
[ApiController]
[Route("api/users")]
[ManagementOnly]
public sealed class UsersController(AppDbContext db, CurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<UserResponse>> List(CancellationToken ct) =>
        (await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct)).Select(UserResponse.From).ToList();

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var email = Domain.Users.User.NormaliseEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw new DomainException("Der findes allerede en bruger med den e-mail.");

        var user = Domain.Users.User.Create(email, request.Name, request.Role, request.Phone);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return UserResponse.From(user);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        if (user.Id == currentUser.User.Id && (!request.IsActive || request.Role != user.Role))
        {
            throw new DomainException("Du kan ikke ændre din egen rolle eller lukke din egen adgang.");
        }

        user.Update(request.Name, request.Role, request.Phone, request.IsActive);
        await db.SaveChangesAsync(ct);
        return UserResponse.From(user);
    }
}
