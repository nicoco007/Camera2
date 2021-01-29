﻿using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Camera2.Interfaces;
using Camera2.Utils;
using Camera2.HarmonyPatches;

namespace Camera2.Configuration {
	class Settings_Smoothfollow {
		public float position = 10f;
		public float rotation = 5f;

		public bool forceUpright = false;

		[JsonIgnore]
		internal Transform parent;
	}
}

namespace Camera2.Middlewares {
	class Smoothfollow : CamMiddleware, IMHandler {
		Scene lastScene;

		Transform parent { get { return settings.Smoothfollow.parent; } set { settings.Smoothfollow.parent = value; } }

		new public bool Pre() {
			if(settings.type == Configuration.CameraType.Positionable)
				return true;

			var parentToUse = ScoresaberUtil.replayCamera == null ? parent : ScoresaberUtil.replayCamera.transform;

			// Need to EXPLICITLY null check before, chaining parent?. breaks if parent is null, idk, unity
			bool checkCamDed() { return parentToUse == null || parentToUse.gameObject?.activeInHierarchy != true; }

			if(checkCamDed()) {
				// If we are not a FP cam we cannot auto-retrieve what we're supposed to be attached to
				if(settings.type != Configuration.CameraType.FirstPerson)
					return false;
				
				parentToUse = parent = Camera.main?.transform ?? HookFPFC.cameraInstance?.transform;

				// If our parent doesnt exist we might as well not render
				if(checkCamDed())
					return false;
			}

			var targetRotation = parentToUse.rotation;

			if(settings.Smoothfollow.forceUpright) {
				float zVal;
				if(SceneUtil.songWorldTransform != null) {
					/*
					 * Substract the world rotation so that the only thing we "correct" for being upright is the HMD
					 * E.g. Map turns you upside down - The view should still be upside down, but "upright" (No rotation other than the maps one)
					 */
					var isolatedHmdRotation = targetRotation * Quaternion.Inverse(SceneUtil.songWorldTransform.rotation);
					zVal = isolatedHmdRotation.eulerAngles.z;
				} else {
					zVal = targetRotation.eulerAngles.z;
				}

				targetRotation *= Quaternion.Euler(0, 0, -zVal);
			}

			// If we switched scenes (E.g. left / entered a song) we want to snap to the correct position before smoothing again
			if(lastScene != SceneUtil.currentScene || (HookFPFC.instance?.enabled == true && !ScoresaberUtil.isInReplay)) {
				cam.transform.SetPositionAndRotation(parentToUse.position, targetRotation);

				lastScene = SceneUtil.currentScene;
			} else {
				cam.transform.position = Vector3.Lerp(cam.transform.position, parentToUse.position, cam.timeSinceLastRender * settings.Smoothfollow.position);
				cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRotation, cam.timeSinceLastRender * settings.Smoothfollow.rotation);
			}
			return true;
		}
	}
}