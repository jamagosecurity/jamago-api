using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Profiles;
using Jama.Domain.Entities;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>A person's own picture and designation; an administrator may edit anyone's, nobody else may.</summary>
public class ProfileTests
{
    private sealed class FakeUser(Guid id, string role = Roles.Staff) : ICurrentUser
    {
        public Guid UserId { get; } = id;
        public string DisplayName => "x";
        public string Role { get; } = role;
        public bool Has(string permission) => false;
        public bool IsSuperAdmin => false;
    }

    private sealed class MemoryStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public async Task<string> SaveAsync(Stream content, string key, CancellationToken ct)
        {
            using var copy = new MemoryStream(); await content.CopyToAsync(copy, ct); Files[key] = copy.ToArray(); return key;
        }
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
            Task.FromResult<Stream?>(Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);
        public Task DeleteAsync(string key, CancellationToken ct) { Files.Remove(key); return Task.CompletedTask; }
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3];

    private static async Task<(ApplicationDbContext Context, AdminUser Owner)> SeedAsync()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"prof-{Guid.NewGuid()}").Options);
        var owner = new AdminUser { Id = Guid.NewGuid(), Email = "a@x.test", FullName = "Aisha", Role = Roles.Staff };
        context.AdminUsers.Add(owner);
        await context.SaveChangesAsync();
        return (context, owner);
    }

    private static SetProfilePhotoCommand Photo(Guid id, byte[] bytes, string name = "me.png") =>
        new() { UserId = id, FileName = name, ContentType = "image/png", SizeBytes = bytes.Length, Content = new MemoryStream(bytes) };

    [Fact]
    public async Task A_person_writes_their_own_designation_and_it_is_trimmed()
    {
        var (context, owner) = await SeedAsync();
        var result = await new UpdateProfileCommandHandler(context, new FakeUser(owner.Id), TimeProvider.System)
            .Handle(new UpdateProfileCommand(owner.Id, "  Senior Technician  "), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Senior Technician", (await context.AdminUsers.SingleAsync()).Designation);
    }

    [Fact]
    public async Task A_phone_number_is_saved_with_the_designation_and_a_blank_one_clears_it()
    {
        var (context, owner) = await SeedAsync();
        var handler = new UpdateProfileCommandHandler(context, new FakeUser(owner.Id), TimeProvider.System);

        var saved = await handler.Handle(new UpdateProfileCommand(owner.Id, "Engineer", " +974 5555 1234 "), default);
        Assert.Equal("+974 5555 1234", saved.Data!.Phone);
        Assert.Equal("+974 5555 1234", (await context.AdminUsers.SingleAsync()).Phone);

        await handler.Handle(new UpdateProfileCommand(owner.Id, "Engineer", ""), default);
        Assert.Null((await context.AdminUsers.SingleAsync()).Phone);
    }

    [Theory]
    [InlineData("+974 5555 1234", true)]
    [InlineData("55551234", true)]
    [InlineData("+974-5555-1234", true)]
    [InlineData("call me", false)]
    [InlineData("12345", false)]
    [InlineData("+974 5555 1234 ext 9", false)]
    [InlineData("1234567890123456", false)]
    public void Only_real_phone_numbers_are_accepted(string phone, bool valid) =>
        Assert.Equal(valid, new UpdateProfileCommandValidator().Validate(new UpdateProfileCommand(Guid.NewGuid(), null, phone)).IsValid);

    [Fact]
    public async Task A_blank_designation_clears_it()
    {
        var (context, owner) = await SeedAsync();
        owner.Designation = "Old"; await context.SaveChangesAsync();
        await new UpdateProfileCommandHandler(context, new FakeUser(owner.Id), TimeProvider.System).Handle(new UpdateProfileCommand(owner.Id, "   "), default);
        Assert.Null((await context.AdminUsers.SingleAsync()).Designation);
    }

    [Fact]
    public async Task Someone_else_cannot_edit_it_but_an_administrator_can()
    {
        var (context, owner) = await SeedAsync();
        var stranger = await new UpdateProfileCommandHandler(context, new FakeUser(Guid.NewGuid()), TimeProvider.System).Handle(new UpdateProfileCommand(owner.Id, "Boss"), default);
        Assert.False(stranger.Succeeded);
        Assert.Null((await context.AdminUsers.SingleAsync()).Designation);

        var admin = await new UpdateProfileCommandHandler(context, new FakeUser(Guid.NewGuid(), Roles.Admin), TimeProvider.System).Handle(new UpdateProfileCommand(owner.Id, "Operations Manager"), default);
        Assert.True(admin.Succeeded);
        Assert.Equal("Operations Manager", (await context.AdminUsers.SingleAsync()).Designation);
    }

    [Fact]
    public void The_designation_must_be_short_and_one_line()
    {
        var validator = new UpdateProfileCommandValidator();
        Assert.True(validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), "Lead CCTV Engineer")).IsValid);
        Assert.False(validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), new string('x', 101))).IsValid);
        Assert.False(validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), "Line one\nLine two")).IsValid);
    }

    [Fact]
    public async Task A_picture_is_stored_versioned_and_replacing_it_removes_the_old_file()
    {
        var (context, owner) = await SeedAsync();
        var storage = new MemoryStorage();
        var handler = new SetProfilePhotoCommandHandler(context, storage, new FakeUser(owner.Id), TimeProvider.System);

        var first = await handler.Handle(Photo(owner.Id, Png), default);
        Assert.True(first.Succeeded);
        Assert.True(first.Data!.PhotoVersion > 0);
        var oldKey = (await context.AdminUsers.SingleAsync()).PhotoKey!;
        Assert.Single(storage.Files);

        await handler.Handle(Photo(owner.Id, Png), default);
        var newKey = (await context.AdminUsers.SingleAsync()).PhotoKey!;
        Assert.NotEqual(oldKey, newKey);
        Assert.Single(storage.Files);                       // the old file is gone
        Assert.True(storage.Files.ContainsKey(newKey));
    }

    [Fact]
    public async Task A_file_that_is_not_the_picture_it_claims_to_be_is_refused_and_nothing_is_stored()
    {
        var (context, owner) = await SeedAsync();
        var storage = new MemoryStorage();
        var result = await new SetProfilePhotoCommandHandler(context, storage, new FakeUser(owner.Id), TimeProvider.System)
            .Handle(Photo(owner.Id, [0x4D, 0x5A, 1, 2, 3, 4, 5, 6, 7, 8]), default);   // an .exe renamed .png

        Assert.False(result.Succeeded);
        Assert.Empty(storage.Files);
        Assert.Null((await context.AdminUsers.SingleAsync()).PhotoKey);
    }

    [Fact]
    public void Only_small_pictures_of_the_usual_types_are_accepted()
    {
        var validator = new SetProfilePhotoCommandValidator();
        Assert.True(validator.Validate(Photo(Guid.NewGuid(), Png)).IsValid);
        Assert.False(validator.Validate(Photo(Guid.NewGuid(), Png, "me.exe")).IsValid);
        Assert.False(validator.Validate(new SetProfilePhotoCommand { UserId = Guid.NewGuid(), FileName = "me.png", SizeBytes = 6 * 1024 * 1024 }).IsValid);
        Assert.False(validator.Validate(new SetProfilePhotoCommand { UserId = Guid.NewGuid(), FileName = "me.png", SizeBytes = 0 }).IsValid);
    }

    [Fact]
    public async Task Someone_else_cannot_change_a_picture_but_the_owner_can_remove_theirs_and_the_file_goes()
    {
        var (context, owner) = await SeedAsync();
        var storage = new MemoryStorage();
        await new SetProfilePhotoCommandHandler(context, storage, new FakeUser(owner.Id), TimeProvider.System).Handle(Photo(owner.Id, Png), default);

        var denied = await new SetProfilePhotoCommandHandler(context, storage, new FakeUser(Guid.NewGuid()), TimeProvider.System).Handle(Photo(owner.Id, Png), default);
        Assert.False(denied.Succeeded);
        Assert.False((await new RemoveProfilePhotoCommandHandler(context, storage, new FakeUser(Guid.NewGuid()), TimeProvider.System).Handle(new RemoveProfilePhotoCommand(owner.Id), default)).Succeeded);

        var removed = await new RemoveProfilePhotoCommandHandler(context, storage, new FakeUser(owner.Id), TimeProvider.System).Handle(new RemoveProfilePhotoCommand(owner.Id), default);
        Assert.True(removed.Succeeded);
        Assert.Equal(0, removed.Data!.PhotoVersion);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task The_picture_can_be_read_back_and_a_missing_one_says_so()
    {
        var (context, owner) = await SeedAsync();
        var storage = new MemoryStorage();
        Assert.False((await new GetProfilePhotoQueryHandler(context, storage).Handle(new GetProfilePhotoQuery(owner.Id), default)).Succeeded);

        await new SetProfilePhotoCommandHandler(context, storage, new FakeUser(owner.Id), TimeProvider.System).Handle(Photo(owner.Id, Png), default);
        var read = await new GetProfilePhotoQueryHandler(context, storage).Handle(new GetProfilePhotoQuery(owner.Id), default);
        Assert.True(read.Succeeded);
        Assert.Equal("image/png", read.Data!.ContentType);
    }
}
