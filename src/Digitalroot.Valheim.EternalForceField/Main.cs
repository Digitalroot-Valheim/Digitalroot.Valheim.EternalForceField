using BepInEx;
using BepInEx.Configuration;
using Digitalroot.Modding.Framework.Logging;
using JetBrains.Annotations;
using Jotunn.Utils;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DMF = Digitalroot.Modding.Framework;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Digitalroot.Valheim.EternalForceField
{
  [BepInPlugin(Guid, Name, Version)]
  [BepInDependency(Jotunn.Main.ModGuid)]
  [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
  [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
  public partial class Main : BaseUnityPlugin, ITraceableLogging
  {
    public static Main Instance;

    [UsedImplicitly]
    public static ConfigEntry<int> NexusId { get; private set; }

    public static ConfigEntry<float> Size { get; private set; }

    private const string TotemPreFabName = "piece_bone_totem_force_field";

    private const float BaseForceFieldSize = 6f;

    private static Vector3 ForceFieldScale => Vector3.one * (BaseForceFieldSize + Size.Value);

    public Main()
    {
      Instance = this;
      #if DEBUG
      EnableTrace = true;
      Log.RegisterSource(Instance); // Enable logging to a './BepInEx/logs' file.
      Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
      #else
      EnableTrace = false;
      #endif
    }

    [UsedImplicitly]
    private void Awake()
    {
      try
      {
        if (EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
        NexusId = Config.Bind(PluginConfigSection.General, "NexusID", 4424, new ConfigDescription("Nexus mod ID for updates", null, new ConfigurationManagerAttributes { Browsable = false, ReadOnly = true }));
        Size = Config.Bind(PluginConfigSection.Advanced, "ForceFieldSize", 40f, new ConfigDescription("Size of the Force Field", new AcceptableValueRange<float>(1f, 100f), new ConfigurationManagerAttributes { Browsable = true, ReadOnly = false, IsAdminOnly = true, IsAdvanced = true }));
        PrefabManager.OnVanillaPrefabsAvailable += PrefabManager_OnVanillaPrefabsAvailable;
        Size.SettingChanged += Size_SettingChanged;
      }
      catch (Exception e)
      {
        Log.Error(Instance, e);
      }
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
      try
      {
        if (EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
        PrefabManager.OnVanillaPrefabsAvailable -= PrefabManager_OnVanillaPrefabsAvailable;
        Size.SettingChanged -= Size_SettingChanged;
      }
      catch (Exception e)
      {
        Log.Error(Instance, e);
      }
    }

    private static GameObject CreateKitbashedPrefab()
    {
      if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
      var totem = PrefabManager.Instance.CreateClonedPrefab(TotemPreFabName, DMF.Names.Vanilla.PrefabNames.GoblinTotempole);

      if (!totem.GetComponent<Piece>())
      {
        totem.AddComponent<Piece>();
      }

      var kb = KitbashManager.Instance.AddKitbash(totem, new KitbashConfig
      {
        FixReferences = true
        , KitbashSources = new List<KitbashSourceConfig>
        {
          new()
          {
            Name = PluginConfigSection.ForceField
            , SourcePrefab = DMF.Names.Vanilla.PrefabNames.HildirCamp
            , SourcePath = PluginConfigSection.ForceField
            , Position = new Vector3(0f, -0.5f, 0f)
            , Rotation = Quaternion.identity
            , Scale = ForceFieldScale
          }
        }
      });

      kb.OnKitbashApplied += () =>
                             {
                               var forceField = totem.transform.Find(PluginConfigSection.ForceField);
                               if (forceField == null) return;
                               forceField.localScale = ForceFieldScale;

                               const float factor = 0.5f; // 1 = unchanged, 0 = no refraction
                               const string targetProperty = "_RefractionIntensity";
                               foreach (var r in forceField.GetComponentsInChildren<MeshRenderer>(true))
                               {
                                 var mats = r.sharedMaterials.Select(m => new Material(m)).ToArray();

                                 foreach (var mat in mats)
                                 {
                                   if (!mat.HasProperty(targetProperty)) continue;

                                   var old = mat.GetFloat(targetProperty);
                                   mat.SetFloat(targetProperty, old * factor);
                                   if (Instance.EnableTrace) Log.Trace(Instance, $"{nameof(Main)}.{nameof(CreateKitbashedPrefab)} [{old} -> {old * factor}]");
                                 }

                                 r.sharedMaterials = mats;
                               }
                             };

      var pickable = totem.GetComponent<Pickable>();
      if (pickable) DestroyImmediate(pickable); // Remove Pickable
      var dropOnDestroyed = totem.GetComponent<DropOnDestroyed>();
      if (dropOnDestroyed) DestroyImmediate(dropOnDestroyed); // Remove extra wood that DropOnDestroyed

      // Copy the icon from the vanilla GoblinTotem item
      var totemItem = PrefabManager.Instance.GetPrefab(DMF.Names.Vanilla.ItemDropNames.GoblinTotem);
      var itemDrop = totemItem ? totemItem.GetComponent<ItemDrop>() : null;
      var icons = itemDrop?.m_itemData?.m_shared?.m_icons;

      if (icons?.Length > 0)
      {
        totem.GetComponent<Piece>().m_icon = icons[0];
      }

      var newColor = new Color(0.2f, 0.9f, 1f, 0.25f); // Cyan

      var particles = totem.GetComponentsInChildren<Transform>(true)
                           // ReSharper disable once StringLiteralTypo
                           .Where(t => t.name is "flare" or "sparcs (1)");

      foreach (var flare in particles)
      {
        if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{flare.name}]");
        if (flare != null)
        {
          foreach (var particleSystem in flare.GetComponentsInChildren<ParticleSystem>(true))
          {
            // Start color
            var particleSystemMain = particleSystem.main;
            particleSystemMain.startColor = newColor;

            // Color over lifetime, if enabled, this can override the start color
            var colorOverLifetime = particleSystem.colorOverLifetime;
            if (colorOverLifetime.enabled)
            {
              // Keep the original alpha fade, swap only the color
              var oldGradient = colorOverLifetime.color.gradient;
              var gradient = new Gradient();
              gradient.SetKeys(new[]
                               {
                                 new GradientColorKey(newColor, 0f)
                                 , new GradientColorKey(newColor, 1f)
                               },
                               oldGradient != null
                                 ? oldGradient.alphaKeys
                                 : new[]
                                 {
                                   new GradientAlphaKey(1f, 0f)
                                   , new GradientAlphaKey(0f, 1f)
                                 });
              colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
            }
          }
        }
      }

      var target = totem.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(t => t.name == "Totem");

      if (target != null)
      {
        MoveChildrenUpAndDisable(target, new[] { "model", "Point light" }); // Removes the Pickable Totem but keeps the glow.
      }

      return totem;
    }

    private static void MoveChildrenUpAndDisable(Transform target, IEnumerable<string> skipList)
    {
      try
      {
        if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
        var newParent = target.parent;
        if (newParent == null) return; // nothing above it to move to

        // Copy the list first, reparenting changes the child collection
        var children = new List<Transform>();
        var enumerable = skipList.ToList();
        foreach (Transform child in target)
        {
          if (!enumerable.Contains(child.name))
          {
            children.Add(child);
          }
        }

        foreach (var child in children)
        {
          if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}[{child.name}]");
          child.SetParent(newParent, true);
        }

        target.gameObject.SetActive(false);
      }
      catch (Exception e)
      {
        Log.Error(Instance, e);
      }
    }

    private void PrefabManager_OnVanillaPrefabsAvailable()
    {
      try
      {
        if (EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");

        var totem = CreateKitbashedPrefab();

        // Add as placeable
        var customPiece = new CustomPiece(totem, true, new PieceConfig
        {
          PieceTable = DMF.Names.Vanilla.PrefabNames.Hammer
          , Name = "$piece_bone_totem_force_field"
          , Description = "$piece_bone_totem_force_field_desc"
          , Usage = new[] { "Misc", "Defense", "Furniture" }
          , Requirements = new[]
          {
            new RequirementConfig(DMF.Names.Vanilla.ItemDropNames.Wood, 10)
            , new RequirementConfig(DMF.Names.Vanilla.ItemDropNames.BoneFragments, 10)
            , new RequirementConfig(DMF.Names.Vanilla.ItemDropNames.HardAntler, 1)
          }
        });

        PieceManager.Instance.AddPiece(customPiece);
      }
      catch (Exception e)
      {
        Log.Error(Instance, e);
      }
      finally
      {
        PrefabManager.OnVanillaPrefabsAvailable -= PrefabManager_OnVanillaPrefabsAvailable;
      }
    }

    private void Size_SettingChanged(object sender, EventArgs e)
    {
      try
      {
        if (EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
        var prefab = PrefabManager.Instance.GetPrefab(TotemPreFabName);
        if (prefab != null)
        {
          var ff = prefab.transform.Find(PluginConfigSection.ForceField);
          if (ff != null)
          {
            if (EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [Updating prefab scale]");
            ff.localScale = ForceFieldScale;
          }
        }

        if (ZNetScene.instance != null && ZNetScene.instance.isActiveAndEnabled)
        {
          var i = 0;
          var hash = TotemPreFabName.GetStableHashCode();
          foreach (var view in ZNetScene.instance.m_instances.Values.Where(v => v && v.GetZDO()?.GetPrefab() == hash))
          {
            if (EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{i}:{view.name}]");

            var ff = view.gameObject.transform.Find(PluginConfigSection.ForceField);

            if (ff != null)
            {
              if (EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{i}:{ff.localScale}]");
              ff.localScale = ForceFieldScale;
              if (EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{i}:{ff.localScale}]");
            }

            i++;
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error(Instance, ex);
      }

    }

    #region Implementation of ITraceableLogging

    /// <inheritdoc />
    public string Source => Namespace;

    /// <inheritdoc />
    public bool EnableTrace { get; }

    #endregion
  }
}
