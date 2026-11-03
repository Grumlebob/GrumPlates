using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace GrumPlates.Features.Accounts.Blazor;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AccountPasswordLengthAttribute : ValidationAttribute
{
    public AccountPasswordLengthAttribute()
        : base("Use 15–128 Unicode characters without control characters.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not string password || string.IsNullOrEmpty(password)) return true;
        if (password.Length > 256) return false;

        var remaining = password.AsSpan();
        var count = 0;
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done || Rune.IsControl(rune))
            {
                return false;
            }
            if (++count > 128) return false;
            remaining = remaining[consumed..];
        }

        return count >= 15;
    }
}
