#region license
/*
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 * 
 * SCANspaceWeather - Space Weather & Magnetosphere Sounder (CSES ZhangHeng-1 / Van Allen Probes)
 */
#endregion

using System;
using System.Text;
using UnityEngine;

namespace SCANsat.SCAN_PartModules
{
	public class SCANspaceWeather : PartModule
	{
		[KSPField(isPersistant = true, guiActive = true, guiName = "空间物理探测")]
		public string spaceWeatherStatus = "磁通门三轴探针工作中";

		[KSPField(isPersistant = false, guiActive = true, guiName = "空间磁场强度(B)", groupName = "SpaceWeather", groupDisplayName = "⚡ 空间环境与地磁电离层")]
		public string magFieldStr = "探测中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "电离层电子总量(TEC)", groupName = "SpaceWeather", groupDisplayName = "⚡ 空间环境与地磁电离层")]
		public string tecStr = "探测中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "高能带电粒子通量", groupName = "SpaceWeather", groupDisplayName = "⚡ 空间环境与地磁电离层")]
		public string particleFluxStr = "计数中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "地磁扰动状态(Kp)", groupName = "SpaceWeather", groupDisplayName = "⚡ 空间环境与地磁电离层")]
		public string kpIndexStr = "评估中...";

		[KSPField]
		public float updateInterval = 1.5f;

		private float lastUpdate = 0f;

		public double magneticField_nT;
		public double tecUnits;
		public double protonFlux; // protons / (cm2 s sr)
		public int kpIndex;
		public string stormLevelDesc = "地磁平静";

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
			if (Time.time - lastUpdate < updateInterval) return;
			lastUpdate = Time.time;

			soundSpaceWeather();
		}

		private void soundSpaceWeather()
		{
			CelestialBody body = vessel.mainBody;
			if (body == null) return;

			double r = (vessel.CoM - body.position).magnitude;
			double altKm = vessel.altitude / 1000.0;
			double R0 = body.Radius;
			double latRad = vessel.latitude * Math.PI / 180.0;

			// Dipole magnetic field model: B = B0 * (R0/r)^3 * sqrt(1 + 3 sin^2(lat))
			// Equatorial surface magnetic field ~31,000 nT for Earth
			double b0 = 31000.0;
			if (body.bodyName.Contains("Jool") || body.bodyName.Contains("Jupiter")) b0 = 420000.0; // giant Jovian field
			else if (body.bodyName.Contains("Mun") || body.bodyName.Contains("Moon")) b0 = 20.0; // unmagnetized crustal remnant
			else if (body.bodyName.Contains("Duna") || body.bodyName.Contains("Mars")) b0 = 220.0; // crustal magnetic umbrellas

			double rRatio = R0 / r;
			magneticField_nT = b0 * Math.Pow(rRatio, 3.0) * Math.Sqrt(1.0 + 3.0 * Math.Sin(latRad) * Math.Sin(latRad));

			// Ionospheric Total Electron Content (TEC, peak around 300-800 km)
			if (altKm < 1500.0 && body.atmosphere)
			{
				double solarZenith = Math.Cos(latRad) * Math.Max(0.0, Math.Cos(vessel.longitude * Math.PI / 180.0));
				tecUnits = Math.Max(2.0, 15.0 + solarZenith * 35.0);
			}
			else
			{
				tecUnits = 0.5; // exosphere
			}

			// Van Allen radiation belts: inner belt ~1000-6000 km, outer belt ~13000-25000 km
			if (altKm >= 1000.0 && altKm <= 6000.0)
			{
				protonFlux = 2.5e5 * Math.Exp(-Math.Pow((altKm - 3000.0) / 1200.0, 2.0));
			}
			else if (altKm >= 12000.0 && altKm <= 28000.0)
			{
				protonFlux = 8.0e6 * Math.Exp(-Math.Pow((altKm - 18000.0) / 4500.0, 2.0));
			}
			else
			{
				protonFlux = 120.0; // cosmic ray background
			}

			// Space weather storm simulation (Kp index 0-9)
			double ut = Planetarium.GetUniversalTime();
			double stormCycle = Math.Sin(ut / (86400.0 * 27.0) * 2.0 * Math.PI); // 27-day solar rotation cycle
			kpIndex = Mathf.Clamp((int)Math.Round(2.0 + stormCycle * 3.5), 0, 9);

			if (kpIndex <= 2) stormLevelDesc = "Kp 1~2: 地磁宁静 (Quiet)";
			else if (kpIndex <= 4) stormLevelDesc = "Kp 3~4: 地磁微扰 (Unsettled)";
			else if (kpIndex <= 6) stormLevelDesc = "Kp 5~6: ⚠️ 中等磁暴 (Moderate Geomagnetic Storm)";
			else stormLevelDesc = "Kp 7~9: 🚨 特大磁暴 (Severe Storm - 极光大爆发)";

			magFieldStr = string.Format("{0:N0} nT", magneticField_nT);
			tecStr = string.Format("{0:F1} TECU", tecUnits);
			particleFluxStr = protonFlux > 1000.0 ? string.Format("{0:E2} /cm²·s (内/外辐射带)", protonFlux) : string.Format("{0:F0} /cm²·s (宇宙线本底)", protonFlux);
			kpIndexStr = stormLevelDesc;
		}

		[KSPEvent(guiActive = true, guiName = "⚡ 生成空间天气与磁层辐射环境简报", groupName = "SpaceWeather")]
		public void ShowSpaceWeatherBriefing()
		{
			soundSpaceWeather();
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine(string.Format("🛰️ 仪器型号: {0} 空间电磁与带电粒子探测器", part.partInfo.title));
			sb.AppendLine(string.Format("🌌 目标天体: {0} | 轨道高度: {1:N1} km", vessel.mainBody.bodyName, vessel.altitude / 1000.0));
			sb.AppendLine(string.Format("📍 空间坐标: {0:F3}°{1}, {2:F3}°{3}",
				Math.Abs(vessel.latitude), vessel.latitude >= 0 ? "N" : "S",
				Math.Abs(vessel.longitude), vessel.longitude >= 0 ? "E" : "W"));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine("【行星磁层与偶极磁场反演】");
			sb.AppendLine(string.Format(" • 空间三轴合成磁感应强度 (B): {0:N1} nT", magneticField_nT));
			sb.AppendLine(string.Format(" • 磁壳参数 (McIlwain L-shell):  L ≈ {0:F2}", (vessel.altitude + vessel.mainBody.Radius) / vessel.mainBody.Radius / Math.Max(0.1, Math.Pow(Math.Cos(vessel.latitude * Math.PI / 180.0), 2.0))));
			sb.AppendLine("【电离层与空间等离子体】");
			sb.AppendLine(string.Format(" • 电离层总电子含量 (TEC):      {0:F2} TECU", tecUnits));
			sb.AppendLine(string.Format(" • 高能带电粒子积分通量:        {0:E2} 质子/cm²·s·sr", protonFlux));
			sb.AppendLine("【太阳活动与地磁暴监测】");
			sb.AppendLine(string.Format(" • 地磁活动指数与空间天气级别:  {0}", stormLevelDesc));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

			PopupDialog.SpawnPopupDialog(
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new MultiOptionDialog(
					"SCANsatSpaceWeatherBriefing",
					sb.ToString(),
					"⚡ 空间高能粒子与地磁电离层综合物理观测简报",
					HighLogic.UISkin,
					new DialogGUIButton("关闭", null, true)
				),
				false,
				HighLogic.UISkin
			);
		}
	}
}
