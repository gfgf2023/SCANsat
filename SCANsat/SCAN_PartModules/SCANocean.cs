#region license
/*
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 * 
 * SCANocean - Satellite Oceanography & Marine Environment (HY-1 / HY-2 / Sentinel-3/6)
 */
#endregion

using System;
using System.Text;
using UnityEngine;

namespace SCANsat.SCAN_PartModules
{
	public class SCANocean : PartModule
	{
		[KSPField(isPersistant = true, guiActive = true, guiName = "海洋遥感系统")]
		public string oceanStatus = "微波雷达+水色仪工作中";

		[KSPField(isPersistant = false, guiActive = true, guiName = "海浪有效波高(SWH)", groupName = "Oceanography", groupDisplayName = "🌊 海洋动力环境与水色遥感")]
		public string waveHeightStr = "测高中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "海面粗糙度/海况", groupName = "Oceanography", groupDisplayName = "🌊 海洋动力环境与水色遥感")]
		public string seaStateStr = "测定中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "叶绿素-a / 水华", groupName = "Oceanography", groupDisplayName = "🌊 海洋动力环境与水色遥感")]
		public string chlorophyllStr = "光谱反演中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "海面实用盐度(SSS)", groupName = "Oceanography", groupDisplayName = "🌊 海洋动力环境与水色遥感")]
		public string salinityStr = "微波辐射测量中...";

		[KSPField]
		public float updateInterval = 2.0f;

		private float lastUpdate = 0f;

		public double swh; // Significant wave height in meters
		public double chlorophyll; // mg/m3
		public double salinity; // PSU
		public string seaStateDesc = "微浪";

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
			if (Time.time - lastUpdate < updateInterval) return;
			lastUpdate = Time.time;

			soundOcean();
		}

		private void soundOcean()
		{
			CelestialBody body = vessel.mainBody;
			if (body == null) return;

			if (!body.ocean)
			{
				oceanStatus = "天体无液态海洋";
				waveHeightStr = "N/A";
				seaStateStr = "内陆陆表环境";
				chlorophyllStr = "无海洋浮游生物";
				salinityStr = "N/A";
				return;
			}

			// Check if sub-satellite point is over water
			double terrainAlt = SCANUtil.getElevation(body, vessel.longitude, vessel.latitude);
			bool isOverWater = terrainAlt <= 0.0;

			if (!isOverWater)
			{
				oceanStatus = "当前位于陆地上空";
				waveHeightStr = "非海洋水面";
				seaStateStr = "陆地反射";
				chlorophyllStr = "植被生物群落覆盖";
				salinityStr = "陆表无海盐";
				return;
			}

			oceanStatus = "洋面回波追踪锁定";

			double lat = vessel.latitude;
			double lon = vessel.longitude;
			double absLat = Math.Abs(lat);

			// Dynamic wave height simulation (higher in stormy roaring 40s/furious 50s)
			double westWindWave = (absLat >= 40.0 && absLat <= 65.0) ? 3.5 : 1.2;
			double randomFactor = Math.Sin(lat * 0.2 + lon * 0.15 + Planetarium.GetUniversalTime() / 3600.0);
			swh = Math.Max(0.4, westWindWave + randomFactor * 1.8);

			if (swh < 1.25) seaStateDesc = "轻浪 (2级海况)";
			else if (swh < 2.5) seaStateDesc = "中浪 (3级海况)";
			else if (swh < 4.0) seaStateDesc = "大浪 (4级海况)";
			else if (swh < 6.0) seaStateDesc = "巨浪 (5级海况)";
			else seaStateDesc = "⚠️ 狂浪/狂涛 (6~7级严重海况)";

			// Chlorophyll concentration (higher near coasts and upwelling zones)
			double coastFactor = Math.Exp(-Math.Pow((terrainAlt + 500.0) / 400.0, 2));
			chlorophyll = Math.Max(0.05, 0.25 + coastFactor * 1.5 + Math.Abs(randomFactor) * 0.4);

			// Salinity (PSU, around 32 - 37 in Earth oceans)
			salinity = 34.5 + Math.Cos(lat * Math.PI / 60.0) * 1.8 - (absLat > 60.0 ? 2.5 : 0.0);

			waveHeightStr = string.Format("{0:F2} 米", swh);
			seaStateStr = seaStateDesc;
			chlorophyllStr = string.Format("{0:F2} mg/m³ ({1})", chlorophyll, chlorophyll > 1.0 ? "富营养化/优良渔场" : "中低生产力大洋");
			salinityStr = string.Format("{0:F1} PSU (实用盐度)", salinity);
		}

		[KSPEvent(guiActive = true, guiName = "🌊 生成海洋水文与水色环境简报", groupName = "Oceanography")]
		public void ShowOceanBriefing()
		{
			soundOcean();
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine(string.Format("🛰️ 仪器型号: {0} 卫星雷达高度计/海洋水色仪", part.partInfo.title));
			sb.AppendLine(string.Format("🌊 目标天体: {0} | 轨道高度: {1:N1} km", vessel.mainBody.bodyName, vessel.altitude / 1000.0));
			sb.AppendLine(string.Format("📍 测线位置: {0:F3}°{1}, {2:F3}°{3}",
				Math.Abs(vessel.latitude), vessel.latitude >= 0 ? "N" : "S",
				Math.Abs(vessel.longitude), vessel.longitude >= 0 ? "E" : "W"));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine("【海洋动力环境观测】");
			sb.AppendLine(string.Format(" • 测高计有效波高 (SWH):       {0:F2} 米", swh));
			sb.AppendLine(string.Format(" • 世界气象组织海况等级:       {0}", seaStateDesc));
			sb.AppendLine(string.Format(" • 海表散射粗糙度 (Sigma-0):   {0:F2} dB", 14.5 - swh * 1.2));
			sb.AppendLine("【海洋生物水色与盐度水文】");
			sb.AppendLine(string.Format(" • 叶绿素-a 浓度 (Chlorophyll): {0:F3} mg/m³", chlorophyll));
			sb.AppendLine(string.Format(" • 悬浮泥沙与离岸泥沙通量:     {0}", chlorophyll > 0.8 ? "显著悬沙带" : "清澈外海区"));
			sb.AppendLine(string.Format(" • 海面实用盐度 (SSS):          {0:F2} PSU", salinity));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

			PopupDialog.SpawnPopupDialog(
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new MultiOptionDialog(
					"SCANsatOceanBriefing",
					sb.ToString(),
					"🌊 空间海洋动力学与水色环境综合遥感简报",
					HighLogic.UISkin,
					new DialogGUIButton("关闭", null, true)
				),
				false,
				HighLogic.UISkin
			);
		}
	
		[KSPEvent(guiActive = true, guiName = "查看海洋浪谱水色与科学读数", active = true)]
		public void OpenInstrumentsWindow()
		{
			if (global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance != null && !global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance.IsVisible)
			{
				global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance.Open();
			}
		}
}
}
