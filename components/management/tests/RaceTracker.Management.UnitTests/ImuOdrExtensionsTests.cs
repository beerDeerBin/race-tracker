using RaceTracker.Management.Domain.Commands;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// The ODR → Hz resolution that fixes a run's time base (/F54/). Every standard rate maps exactly;
/// the one fractional rate (12.5 Hz) rounds to 13 because the persisted column is an integer.
/// </summary>
public sealed class ImuOdrExtensionsTests
{
    [Theory]
    [InlineData(ImuOdr.Hz12_5, 13)]
    [InlineData(ImuOdr.Hz26, 26)]
    [InlineData(ImuOdr.Hz52, 52)]
    [InlineData(ImuOdr.Hz104, 104)]
    [InlineData(ImuOdr.Hz208, 208)]
    [InlineData(ImuOdr.Hz417, 417)]
    [InlineData(ImuOdr.Hz833, 833)]
    public void Resolves_each_odr_to_its_physical_rate(ImuOdr odr, int expectedHz) =>
        odr.ToHz().ShouldBe(expectedHz);
}
