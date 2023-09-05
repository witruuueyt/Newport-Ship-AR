using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bearroll.UltimateDecals {

	public class UD_Pass {

		public UltimateDecalType type { get; private set; }

		public UD_Manager manager {
			get {
				return UD_Manager.instance;
			}
		}

		List<UD_Batch> batches = new List<UD_Batch>(16);
		Dictionary<UltimateDecal, UD_Batch> batchByDecal = new Dictionary<UltimateDecal, UD_Batch>(16);

		public int batchCount {
			get { return batches.Count; }
		}

		public UD_Pass(UltimateDecalType type, UD_Manager manager) {
			this.type = type;
		}

		public int decalCount {
			get {
				var count = 0;
				foreach (var batch in batches) {
					count += batch.count;
				}
				return count;
			}
		}

		public void RebuildCommandBuffer(UD_Camera camera, CommandBuffer commandBuffer) {

			commandBuffer.Clear();

			camera.decalRenderer.StartPassLitBuffer(commandBuffer);

			foreach (var batch in batches) {

				if (batch.count == 0) continue;

				if (batch.material == null) continue;

				if (!batch.isLit) continue;

				commandBuffer.SetGlobalVectorArray("_Size", batch.sizes);
				commandBuffer.SetGlobalFloatArray("_AtlasIndexOffset", batch.atlasIndices);
				commandBuffer.SetGlobalFloatArray("_CreateTime", batch.ctimes);

				#if UD_LWRP || UD_URP
				var pass = 1;
				#else
				var pass = camera.isForward ? 0 : 2;
				#endif

				commandBuffer.DrawMeshInstanced(ResourceManager.instanceMesh, 0, batch.material, pass, batch.matrices, batch.count, batch.props);

			}

		}

		public void RebuildUnlitCommandBuffer(UD_Camera camera, CommandBuffer commandBuffer) {

			commandBuffer.Clear();

			camera.decalRenderer.StartPassUnlitBuffer(commandBuffer);

			foreach (var batch in batches) { 

				if (batch.count == 0) continue;

				if (batch.material == null) continue;

				if (batch.isLit) continue;
				
				commandBuffer.SetGlobalVectorArray("_Size", batch.sizes);
				commandBuffer.SetGlobalFloatArray("_AtlasIndexOffset", batch.atlasIndices);
				commandBuffer.SetGlobalFloatArray("_CreateTime", batch.ctimes);

				commandBuffer.DrawMeshInstanced(ResourceManager.instanceMesh, 0, batch.material, 0, batch.matrices, batch.count);

			}

		}

		public UD_Batch GetBatchByMaterial(Material material) {

			UD_Batch first = null;
			var limit = manager.maxPermanentMarks;

			for (var i = 0; i < batches.Count; i++) {

				var e = batches[i];

				if (e.material != material) continue;

				if (first == null) first = e;

				if (e.isFull) {

					if (type != UltimateDecalType.PermanentMark) continue;

					limit -= e.count;

					if (limit > 0) continue;

					if (first != null) return first;

					break;

				}

				return e;

			}

			var r = new UD_Batch(manager, type, material);

            var index = 0;
            while (index < batches.Count && batches[index].material.renderQueue < material.renderQueue)
                index++;

			batches.Insert(index, r);

			return r;

		}

		public void AddDecal(UltimateDecal decal) {

			if (decal.material == null) return;

			if (decal.material != null) {
				decal.material.enableInstancing = true;
			}

			var batch = GetBatchByMaterial(decal.material);
				
			batch.AddDecal(decal);

			batchByDecal[decal] = batch;

		}

		public void RemoveDecal(UltimateDecal decal) {

			if (!batchByDecal.ContainsKey(decal)) return;

			batchByDecal[decal].RemoveDecal(decal);

			batchByDecal.Remove(decal);

		}

		public void AddDecal(Material material, Transform t, float normalizedAtlasOffset = 0) {

			GetBatchByMaterial(material).AddDecal(t, normalizedAtlasOffset);

		}

		public void AddDecal(Material material, Matrix4x4 matrix, float normalizedAtlasOffset = 0) {

			GetBatchByMaterial(material).AddDecal(matrix, normalizedAtlasOffset);

		}

		public bool Update() {

			var needsRebuild = false;

			foreach (var e in batches) {
				if (e.Update()) {
					needsRebuild = true;
				}
			}

			return needsRebuild;

		}

		public void Clean() {

			foreach (var batch in batches) {
				batch.Clean();
			}

			batches.Clear();

		}


	}

}