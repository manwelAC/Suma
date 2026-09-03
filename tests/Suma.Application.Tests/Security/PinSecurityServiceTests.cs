using Suma.Application.Abstractions.Security;
using Suma.Application.Common.Exceptions;
using Suma.Application.Security;
using Xunit;

namespace Suma.Application.Tests.Security;

public sealed class PinSecurityServiceTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("12345")]
    [InlineData("123456")]
    public async Task Valid_pin_lengths_enable_and_verify(string pin)
    {
        var store = new MemoryStore(); var service = new PinSecurityService(store); await service.EnableAsync(pin, pin, Token);
        Assert.True(await service.VerifyAsync(pin, Token)); Assert.False(await service.VerifyAsync("9999", Token)); Assert.Equal((1, true, PinSecurityService.Algorithm, PinSecurityService.Iterations), (store.Value.Version, store.Value.Enabled, store.Value.Algorithm, store.Value.Iterations));
        Assert.Equal(16, Convert.FromBase64String(store.Value.Salt!).Length); Assert.Equal(32, Convert.FromBase64String(store.Value.Hash!).Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1234567")]
    [InlineData("12a4")]
    [InlineData("12 4")]
    [InlineData("12-4")]
    public async Task Invalid_pin_is_rejected(string pin) => await Assert.ThrowsAsync<ApplicationValidationException>(() => new PinSecurityService(new MemoryStore()).EnableAsync(pin, pin, Token));

    [Fact]
    public async Task Confirmation_must_match_and_each_credential_has_new_salt()
    {
        var first = new MemoryStore(); var second = new MemoryStore(); await new PinSecurityService(first).EnableAsync("1234", "1234", Token); await new PinSecurityService(second).EnableAsync("1234", "1234", Token);
        Assert.NotEqual(first.Value.Salt, second.Value.Salt); Assert.NotEqual(first.Value.Hash, second.Value.Hash);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => new PinSecurityService(new MemoryStore()).EnableAsync("1234", "1235", Token));
    }

    [Fact]
    public async Task Change_requires_current_pin_rotates_salt_and_preserves_old_credential_on_failure()
    {
        var store = new MemoryStore(); var service = new PinSecurityService(store); await service.EnableAsync("1234", "1234", Token); var original = store.Value;
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.ChangeAsync("9999", "5678", "5678", Token)); Assert.Equal(original, store.Value); Assert.True(await service.VerifyAsync("1234", Token));
        await service.ChangeAsync("1234", "5678", "5678", Token); Assert.NotEqual(original.Salt, store.Value.Salt); Assert.False(await service.VerifyAsync("1234", Token)); Assert.True(await service.VerifyAsync("5678", Token));
    }

    [Fact]
    public async Task Disable_requires_current_pin_and_write_failures_preserve_state()
    {
        var store = new MemoryStore(); var service = new PinSecurityService(store); await service.EnableAsync("1234", "1234", Token); var original = store.Value;
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.DisableAsync("9999", Token)); Assert.Equal(original, store.Value);
        store.FailWrites = true; await Assert.ThrowsAsync<IOException>(() => service.ChangeAsync("1234", "5678", "5678", Token)); Assert.Equal(original, store.Value); await Assert.ThrowsAsync<IOException>(() => service.DisableAsync("1234", Token)); Assert.Equal(original, store.Value);
        store.FailWrites = false; await service.DisableAsync("1234", Token); Assert.False(await service.IsEnabledAsync(Token)); Assert.True(await service.VerifyAsync("anything", Token)); Assert.Null(store.Value.Salt); Assert.Null(store.Value.Hash);
    }

    [Fact]
    public async Task Enable_write_failure_leaves_disabled_state()
    {
        var store = new MemoryStore { FailWrites = true }; await Assert.ThrowsAsync<IOException>(() => new PinSecurityService(store).EnableAsync("1234", "1234", Token)); Assert.False(store.Value.Enabled);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class MemoryStore : ISecuritySettingsStore
    {
        public SecuritySettings Value { get; private set; } = SecuritySettings.Disabled; public bool FailWrites { get; set; }
        public Task<SecuritySettings> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task WriteAsync(SecuritySettings settings, CancellationToken cancellationToken = default) { if (FailWrites) throw new IOException(); Value = settings; return Task.CompletedTask; }
    }
}
