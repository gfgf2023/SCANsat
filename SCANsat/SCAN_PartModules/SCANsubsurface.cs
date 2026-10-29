#region license
/*
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 * 
 * SCANsubsurface - Ground Penetrating Radar (GPR) for Planetary Subsurface Sounding
 * Inspired by Tianwen-1 RoPeR, Chang'E LPR, Mars Express MARSIS, MRO SHARAD
 */
#endregion

using System;
using System.Text;
using UnityEngine;

namespace SCANsat.SCAN_PartModules
{
	public class SCANsubsurface : PartModule
	{
		[KSPField(isPersistant = true, guiActive = true, guiName = "次表层雷达")]
		public string radarStatus = "低频脉冲发射中";

		[KSPField(isPersistant = false, guiActive = true, guiName = "表层风化层厚度", groupName = "Subsurface", groupDisplayName = "📡 次表层地质雷达测深")]
		public string regolithDepthStr = "测深中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "基岩隐伏深度", groupName = "Subsurface", groupDisplayName = "📡 次表层地质雷达测深")]
		public string bedrockDepthStr = "测深中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "地下界面介电常数", groupName = "Subsurface", groupDisplayName = "📡 次表层地质雷达测深")]
		public string dielectricStr = "反演中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "地下冰层/溶洞识别", groupName = "Subsurface", groupDisplayName = "📡 次表层地质雷达测深")]
		public string featureStr = "解算中...";

		[KSPField]
		public float maxPenetrationMeters = 3500.0f; // e.g. 3.5 km for MARSIS / RoPeR

		[KSPField]
		public float updateInterval = 2.0f;

		private float lastUpdate = 0f;

		public double regolithThickness;
		public double bedrockDepth;
		public double dielectricConstant;
		public string detectedFeature = "干燥致密基岩层";

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
			if (Time.time - lastUpdate < updateInterval) return;
			lastUpdate = Time.time;

			soundSubsurface();
		}

		private void soundSubsurface()
		{
			CelestialBody body = vessel.mainBody;
			if (body == null) return;

			double lat = vessel.latitude;
			double lon = vessel.longitude;
			double absLat = Math.Abs(lat);

			// Procedural subsurface structure based on celestial body & location
			double seed = Math.Sin(lat * 0.12345) * Math.Cos(lon * 0.08765);
			regolithThickness = Math.Max(1.5, 4.5 + seed * 3.5);

			bool isPolar = absLat > 60.0;
			bool isIcyWorld = body.bodyName.Contains("Minmus") || body.bodyName.Contains("Vall") || body.bodyName.Contains("Eeloo")
				|| body.bodyName.Contains("Europa") || body.bodyName.Contains("Enceladus") || body.bodyName.Contains("Callisto")
				|| body.bodyName.Contains("Ganymede");

			if (isIcyWorld)
			{
				dielectricConstant = 3.15; // pure water ice
				bedrockDepth = Math.Min(maxPenetrationMeters, 1200.0 + seed * 800.0);
				detectedFeature = "❄️ 纯度 > 90% 的巨厚深部水冰层 (基底液态水可能)";
			}
			else if (isPolar)
			{
				dielectricConstant = 3.25; // ice/dust mix
				bedrockDepth = Math.Min(maxPenetrationMeters, 280.0 + seed * 180.0);
				detectedFeature = "❄️ 极区浅埋水冰混合沉积层 (冰透镜体)";
			}
			else if (body.ocean)
			{
				dielectricConstant = 8.5; // wet sediment/crust
				bedrockDepth = Math.Min(maxPenetrationMeters, 150.0 + seed * 90.0);
				detectedFeature = "含水渗透沉积砂岩层与水化矿物";
			}
			else
			{
				// Basaltic dry volcanic regolith
				dielectricConstant = 5.8 + seed * 1.2;
				bedrockDepth = Math.Min(maxPenetrationMeters, 65.0 + seed * 45.0);
				if (Math.Abs(seed) > 0.85)
				{
					detectedFeature = "🕳️ 地下隐伏熔岩管道/空洞 (Lava Tube 潜在基地选址)";
				}
				else
				{
					detectedFeature = "干燥多孔火山角砾岩与玄武岩致密基底";
				}
			}

			regolithDepthStr = string.Format("{0:F1} 米", regolithThickness);
			bedrockDepthStr = string.Format("{0:F0} 米 (最大探测深: {1:F0}m)", bedrockDepth, maxPenetrationMeters);
			dielectricStr = string.Format("εr = {0:F2}", dielectricConstant);
			featureStr = detectedFeature;
		}

		[KSPEvent(guiActive = true, guiName = "📡 生成次表层地质雷达剖面简报", groupName = "Subsurface")]
		public void ShowSubsurfaceBriefing()
		{
			soundSubsurface();
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine(string.Format("🛰️ 仪器型号: {0} 甚高频/低频探地雷达(GPR)", part.partInfo.title));
			sb.AppendLine(string.Format("🪐 探测天体: {0} | 轨道高度: {1:N1} km", vessel.mainBody.bodyName, vessel.altitude / 1000.0));
			sb.AppendLine(string.Format("📍 测线位置: {0:F3}°{1}, {2:F3}°{3}",
				Math.Abs(vessel.latitude), vessel.latitude >= 0 ? "N" : "S",
				Math.Abs(vessel.longitude), vessel.longitude >= 0 ? "E" : "W"));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine("【电磁波介质电特性反演】");
			sb.AppendLine(string.Format(" • 次表层等效介电常数 (εr):      {0:F2}", dielectricConstant));
			sb.AppendLine(string.Format(" • 电磁波浅层地中衰减率:         {0:F2} dB/m", (dielectricConstant - 3.0) * 0.15 + 0.05));
			sb.AppendLine("【地层分层与穿透界面】");
			sb.AppendLine(string.Format(" • 表层风化碎屑层厚度 (Regolith): {0:F1} 米", regolithThickness));
			sb.AppendLine(string.Format(" • 第一波阻抗界面(强反射基岩):    {0:F1} 米", bedrockDepth));
			sb.AppendLine(string.Format(" • 地质构造与水冰判识结论:       {0}", detectedFeature));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

			PopupDialog.SpawnPopupDialog(
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new MultiOptionDialog(
					"SCANsatSubsurfaceBriefing",
					sb.ToString(),
					"📡 深空次表层穿透雷达层析成像分析简报",
					HighLogic.UISkin,
					new DialogGUIButton("关闭", null, true)
				),
				false,
				HighLogic.UISkin
			);
		}
	
		[KSPEvent(guiActive = true, guiName = "查看探地雷达剖面与科学读数", active = true)]
		public void OpenInstrumentsWindow()
		{
			if (global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance != null && !global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance.IsVisible)
			{
				global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance.Open();
			}
		}
}
}
