﻿using UnityEngine;
using Camera2.Utils;
using Camera2.Interfaces;
using Camera2.Managers;

namespace Camera2.Configuration {
	class Settings_ModmapExtensions {
		public bool moveWithMap = true;
		public bool autoOpaqueWalls = false;
	}
}

namespace Camera2.Middlewares {
	class ModmapExtensions : CamMiddleware, IMHandler {
		private Transform attachedTo = null;
		public new bool Pre() {
			// We wanna parent FP cams as well so that the noodle translations are applied instantly and dont get smoothed out by SmoothFollow
			if(
				enabled && settings.ModmapExtensions.moveWithMap && 
				!SceneUtil.isInMenu && 
				cam.settings.type != Configuration.CameraType.Attached &&
				SceneUtil.songWorldTransform != null
			) {
				// If we are not yet attached, and we dont have a parent thats active yet, try to get one!
				if(attachedTo != SceneUtil.songWorldTransform) {
#if DEBUG
					Plugin.Log.Info($"Enabling Modmap parenting for camera {cam.name}");
#endif
					attachedTo = SceneUtil.songWorldTransform;
					cam.SetParent(SceneUtil.songWorldTransform);
				}
			} else if(attachedTo != null) {
				attachedTo = null;
				cam.SetParent(null);
			}
			return true;
		}

		/*
		 * This gets called when we are leaving a song because otherwise any game object attached
		 * to the origin would get destroyed in the process of the origin being destroyed
		 */
		public static void ForceDetachTracks() {
			foreach(var cam in CamManager.cams.Values) {
				if(cam.settings.type == Configuration.CameraType.Attached)
					continue;

				cam.SetParent(null);
			}
		}
	}
}