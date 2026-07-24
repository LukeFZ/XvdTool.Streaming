using System.Collections.Generic;

namespace LibXboxOne.XVC2.Specifiers;

public sealed record PackagingLogicalSpecifier(LogicalSpecifierType Type, List<IPackagingSpecifier> Specifiers) : IPackagingSpecifier;