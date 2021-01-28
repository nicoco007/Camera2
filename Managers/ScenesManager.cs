﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Camera2.Configuration;
using Camera2.Utils;
using System;

namespace Camera2.Managers {

	static class ScenesManager {
		internal static ScenesSettings settings { get; private set; } = new ScenesSettings();
		static SceneTypes loadedScene = SceneTypes.MultiplayerMenu;

		public static void ActiveSceneChanged(string sceneName = "MenuCore") {
			if(!settings.dontAutoswitchFromCustom && (loadedScene == SceneTypes.Custom1 || loadedScene == SceneTypes.Custom2 || loadedScene == SceneTypes.Custom3))
				return;

			if(!settings.enableAutoSwitch)
				return;

#if DEBUG
			Plugin.Log.Info($"ActiveSceneChanged({sceneName}) - Current loadedScene: {loadedScene}");
#endif

			LoadGameScene(sceneName);
		}

		public static void LoadGameScene(string sceneName = "MenuCore") {
			List<SceneTypes> toLookup = new List<SceneTypes> { SceneTypes.Menu };
			
			if(SceneUtil.menuSceneNames.Contains(sceneName)) {
				if(SceneUtil.isInMultiplayer)
					toLookup.Insert(0, SceneTypes.MultiplayerMenu);
			} else if(sceneName == "GameCore") {
				toLookup.Insert(0, SceneTypes.Playing);

				if(ScoresaberUtil.IsInReplay())
					toLookup.Insert(0, SceneTypes.Replay);
				else if(SceneUtil.isInMultiplayer)
					toLookup.Insert(0, SceneTypes.MultiplayerPlaying);
			}

#if DEBUG
			Plugin.Log.Info($"LoadGameScene -> {String.Join(", ", toLookup)}");
#endif

			SwitchToScene(FindSceneToUse(toLookup.ToArray()));
		}

		public static void SwitchToScene(SceneTypes scene) {
			if(!settings.scenes.ContainsKey(scene))
				return;

#if DEBUG
			Plugin.Log.Info($"Switching to scene {scene}");
			Plugin.Log.Info($"Cameras: {String.Join(", ", settings.scenes[scene])}");
#endif
			if(loadedScene == scene)
				return;

			loadedScene = scene;

			SwitchToCamlist(settings.scenes[scene]);
		}

		private static void SwitchToCamlist(List<string> cams) {
			if(cams?.Count == 0)
				cams = null;
			/*
			 * Intentionally checking != false, this way if cams is null OR
			 * it contains it, the cam will be activated, only if its
			 * a non-empty scene we want to hide cams that are not in it
			 */
			foreach(var cam in CamManager.cams)
				cam.Value.gameObject.SetActive(cams?.Contains(cam.Key) != false);

			GL.Clear(true, true, Color.black);
		}

		private static SceneTypes FindSceneToUse(SceneTypes[] types) {
			if(settings.scenes.Count == 0) 
				return SceneTypes.Menu;

			foreach(var type in types) {
				if(settings.scenes[type].Count() > 0)
					return type;
			}
			return SceneTypes.Menu;
		}
	}
}
