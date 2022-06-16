using System;
using System.IO;
using UnityEngine;

namespace ShipbreakerVr;

public static class VrAssetManager
{
	private const string assetsDir = "/BepInEx/plugins/ShipbreakerVr/AssetBundles/";

	public static AssetBundle LoadBundle(string assetName)
	{
		Debug.Log($"loading bundle {assetName}...");
		var bundle = AssetBundle.LoadFromFile(string.Format("{0}{1}{2}", Directory.GetCurrentDirectory(), assetsDir,
			assetName));

		if (bundle == null)
		{
			throw new Exception("Failed to load asset bundle " + assetName);
		}

		Debug.Log($"Loaded bundle {bundle.name}");

		return bundle;
	}
}