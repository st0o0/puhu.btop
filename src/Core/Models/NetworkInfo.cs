namespace Puhu.Btop.Core.Models;

public record NetworkInfo(
    string Id, string Name, string Driver, string Scope,
    bool Internal, bool IPv6, string Subnet,
    IReadOnlyList<NetworkContainer> Containers);

public record NetworkContainer(string ContainerId, string Name, string IPv4Address);
