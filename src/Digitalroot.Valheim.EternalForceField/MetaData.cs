using System.Diagnostics.CodeAnalysis;

namespace Digitalroot.Valheim.EternalForceField
{
  [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
  public partial class Main
  {
    public const string Version = "0.0.1";
    public const string Name = "Eternal Force Field";
    public const string Guid = "digitalroot.eternalforcefield";
    public const string Namespace = $"{nameof(Digitalroot)}.{nameof(Valheim)}.{nameof(EternalForceField)}";

    internal static class PluginConfigSection
    {
      internal static readonly string General = nameof(General);
      internal static readonly string Advanced = nameof(Advanced);
      internal static readonly string ForceField = nameof(ForceField);
    }
  }
}
