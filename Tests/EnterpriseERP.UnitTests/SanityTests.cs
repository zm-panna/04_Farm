using Xunit;using EnterpriseERP.Domain;namespace EnterpriseERP.UnitTests;public class SanityTests{[Fact]public void AnimalDefaultsToUnsold(){Assert.False(new Animal().Sold);}}
