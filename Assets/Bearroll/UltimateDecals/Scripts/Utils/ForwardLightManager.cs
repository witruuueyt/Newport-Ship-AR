using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Bearroll.UltimateDecals {

	public class ForwardLightManager {

		public Light mainDirectionalLight { get; private set; }

		List<Light> lights = new List<Light>();

		static int maxLights = 8;

		Vector4[] lightPos = new Vector4[maxLights];
		Vector4[] lightColor = new Vector4[maxLights];
		Vector4[] lightDir = new Vector4[maxLights];

		bool requiresLightUpdate;
		float[] weights = new float[maxLights];
		Plane[] planes = new Plane[6];

		private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1) {
			requiresLightUpdate = true;
		}

		public ForwardLightManager(UD_Manager manager) {

			requiresLightUpdate = true;

			SceneManager.sceneLoaded += OnSceneLoaded;

		}

		public void Clean() {
			lights.Clear();
		}

		public void PrepareFrame(UD_Camera camera) {

			Profiler.BeginSample("PrepareFrame", camera.gameObject);

			if (requiresLightUpdate) {
				lights.Clear();
				lights.AddRange(FindObjectsOfTypeAll<Light>());
				requiresLightUpdate = false;
			}

			var count = 0;
			Light directionalLight = null;

			GeometryUtility.CalculateFrustumPlanes(camera.camera, planes);

			for(var i = lights.Count - 1; i >= 0; i--) {

				var light = lights[i];

				if(light == null) {
					lights.RemoveAt(i);
					continue;
				}

				if(!light.isActiveAndEnabled) continue;

				if(light.intensity < 0.01f) continue;

				if(light.type == LightType.Directional) {

					if(directionalLight == null || light.intensity > directionalLight.intensity) {
						directionalLight = light;
					}

					continue;

				}

				var transform = light.transform;
				var position = transform.position;
				var bounds = new Bounds(position, Vector3.one * light.range * 2);

				if (!GeometryUtility.TestPlanesAABB(planes, bounds)) continue;

				var weight = light.intensity * light.color.maxColorComponent * light.range * light.spotAngle;

				weight /= Vector3.Distance(light.transform.position, camera.transform.position);

				if(count < maxLights) count++;

				var index = count - 1;
				while(index > 0 && weights[index] < weight) {
					index--;
				}

				for(var j = count - 1; j > index; j--) {

					weights[j] = weights[j - 1];

					lightPos[j] = lightPos[j - 1];
					lightDir[j] = lightDir[j - 1];
					lightColor[j] = lightColor[j - 1];
				}

				var pos = (Vector4) position;
				pos.w = light.range;

				var dir = (Vector4) transform.forward;

				if(light.type == LightType.Spot) {
					dir.w = 1 - Mathf.Cos(Mathf.Deg2Rad * light.spotAngle / 2f);
				} else {
					dir.w = -1;
				}

				weights[index] = weight;
				lightPos[index] = pos;
				lightDir[index] = dir;
				lightColor[index] = GetLightColor(light);

			}

			if(directionalLight != null && directionalLight.bakingOutput.lightmapBakeType != LightmapBakeType.Baked) {

				mainDirectionalLight = directionalLight;

				Shader.SetGlobalVector("UD_MainLightDir", mainDirectionalLight.transform.forward);

				var color = GetLightColor(mainDirectionalLight);
				color.w = mainDirectionalLight.shadowStrength;

				Shader.SetGlobalVector("UD_MainLightColor", color);

			} else {

				mainDirectionalLight = null;

				Shader.SetGlobalVector("UD_MainLightColor", Vector3.zero);

			}

			Shader.SetGlobalFloat("UD_LightCount", count);

			Shader.SetGlobalVectorArray("UD_LightPos", lightPos);
			Shader.SetGlobalVectorArray("UD_LightDir", lightDir);
			Shader.SetGlobalVectorArray("UD_LightColor", lightColor);

			Profiler.EndSample();

		}

		public static List<T> FindObjectsOfTypeAll<T>() {
			var results = new List<T>();
			for(var i = 0; i < SceneManager.sceneCount; i++) {
				var s = SceneManager.GetSceneAt(i);
				if(s.isLoaded) {
					var allGameObjects = s.GetRootGameObjects();
					for(var j = 0; j < allGameObjects.Length; j++) {
						var go = allGameObjects[j];
						results.AddRange(go.GetComponentsInChildren<T>(true));
					}
				}
			}
			return results;
		}

		static Vector4 GetLightColor(Light light) {
			if (QualitySettings.activeColorSpace == ColorSpace.Gamma) {
				return light.color.linear * Mathf.GammaToLinearSpace(light.intensity);				
			} else {
				return light.color * light.intensity;
			}

		}

	}

}