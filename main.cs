using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace HollowSails;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class Plugin : BaseUnityPlugin
{
	public const string ModGUID = "zopthemop.hollowsails";
	public const string ModName = "Hollow Sails";
	public const string ModVersion = "1.0.0";
	public const string ModDescription = "Makes sails mainly see-through (only edges visible)";

	private static readonly HashSet<string> SailTextureNames = new HashSet<string>
	{
		"sail_hide",
		"sail_white",
		"sail_diffuse"
	};

	private static readonly Dictionary<string, Texture2D> TransparentTextures = new Dictionary<string, Texture2D>();

	private void Awake()
	{
		Harmony harmony = new Harmony(ModGUID);
		harmony.PatchAll();
	}

	private static void MakeSailHollow(Ship ship)
	{
		if (ship == null || ship.m_sailObject == null)
		{
			return;
		}

		SkinnedMeshRenderer renderer = ship.m_sailObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
		if (renderer == null)
		{
			return;
		}

		foreach (Material material in renderer.materials)
		{
			if (material == null)
			{
				continue;
			}

			Texture2D texture = material.mainTexture as Texture2D;
			if (texture == null || !SailTextureNames.Contains(texture.name))
			{
				continue;
			}

			material.SetOverrideTag("RenderType", "Transparent");
			material.mainTexture = GetTransparentTexture(texture);
		}
	}

	private static Texture2D GetTransparentTexture(Texture2D original)
	{
		if (TransparentTextures.TryGetValue(original.name, out Texture2D cachedTexture))
		{
			return cachedTexture;
		}

		Texture2D readable = MakeReadableCopy(original);
		Texture2D transparent = CreateHollowTexture(readable);

		transparent.name = original.name + "_transparent";
		transparent.wrapMode = original.wrapMode;
		transparent.filterMode = original.filterMode;
		transparent.anisoLevel = original.anisoLevel;

		TransparentTextures[original.name] = transparent;

		return transparent;
	}

	private static Texture2D MakeReadableCopy(Texture2D texture)
	{
		RenderTexture temporary = RenderTexture.GetTemporary(
			texture.width,
			texture.height,
			0,
			RenderTextureFormat.ARGB32,
			RenderTextureReadWrite.Default
		);

		RenderTexture previous = RenderTexture.active;

		Graphics.Blit(texture, temporary);
		RenderTexture.active = temporary;

		Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
		readable.ReadPixels(new Rect(0, 0, temporary.width, temporary.height), 0, 0);
		readable.Apply();

		RenderTexture.active = previous;
		RenderTexture.ReleaseTemporary(temporary);

		readable.name = texture.name;

		return readable;
	}

	private static Texture2D CreateHollowTexture(Texture2D source)
	{
		Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);

		Color[] pixels = source.GetPixels();

		int width = source.width;
		int height = source.height;

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				int index = y * width + x;
				Color color = pixels[index];

				bool isBorderPixel =
					x < 4 ||
					x >= width - 4 ||
					y < 16 ||
					y >= height - 16;

				// only border pixels of sails are opaque
				color.a = isBorderPixel ? 1f : 0f;

				pixels[index] = color;
			}
		}

		result.SetPixels(pixels);
		result.Apply();

		return result;
	}

	[HarmonyPatch(typeof(Ship), "Awake")]
	private static class ShipAwakePatch
	{
		private static void Postfix(Ship __instance)
		{
			MakeSailHollow(__instance);
		}
	}

	[HarmonyPatch(typeof(Ship), "UpdateSailSize")]
	private static class ShipUpdateSailSizePatch
	{
		private static void Postfix(Ship __instance)
		{
			MakeSailHollow(__instance);
		}
	}
}
