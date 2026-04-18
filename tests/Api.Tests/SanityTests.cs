using FluentAssertions;
using Xunit;

namespace Hackaton.Api.Tests;

public class SanityTests
{
    [Fact]
    public void Toolchain_Is_Wired()
    {
        var sum = 1 + 1;
        sum.Should().Be(2);
    }
}
