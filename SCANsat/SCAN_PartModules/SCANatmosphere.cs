#region license
/*
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 * 
 * SCANatmosphere - Greenhouse Gas & Atmospheric Column Sounder (TanSat / OCO-2 / GOSAT)
 */
#endregion

using System;
using System.Text;
using UnityEngine;

namespace SCANsat.SCAN_PartModules
{
	public class SCANatmosphere : PartModule
	{
		[KSPField(isPersistant = true, guiActive = true, guiName = "温室气体光谱仪")]
		public string carbonStatus = "高光谱吸收谱线反演中";

		[KSPField(isPersistant = false, guiActive = true, guiName = "二氧化碳柱浓度(XCO2)", groupName = "GreenhouseGas", groupDisplayName = "🌱 大气成分与碳汇遥感")]
		public string xco2Str = "探测中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "甲烷柱浓度(XCH4)", groupName = "GreenhouseGas", groupDisplayName = "🌱 大气成分与碳汇遥感")]
		public string xch4Str = "探测中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "气溶胶厚度(AOD)", groupName = "GreenhouseGas", groupDisplayName = "🌱 大气成分与碳汇遥感")]
		public string aodStr = "探测中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "碳通量性质", groupName = "GreenhouseGas", groupDisplayName = "🌱 大气成分与碳汇遥感")]
		public string carbonFluxStr = "分析中...";

		[KSPField]
		public float updateInterval = 2.0f;

		private float lastUpdate = 0f;

		public double xco2; // ppm
		public double xch4; // ppb
		public double aod;
		public double ozoneDU; // Dobson Units
		public string carbonFluxType = "中性背景通量";

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
			if (Time.time - lastUpdate < updateInterval) return;
			lastUpdate = Time.time;

			soundGreenhouseGases();
		}

		private void soundGreenhouseGases()
		{
			CelestialBody body = vessel.mainBody;
			if (body == null) return;

			if (!body.atmosphere)
			{
				carbonStatus = "无大气层天体";
				xco2Str = "N/A";
				xch4Str = "N/A";
				aodStr = "N/A";
				carbonFluxStr = "真空气体为零";
				return;
			}

			double lat = vessel.latitude;
			double lon = vessel.longitude;
			double absLat = Math.Abs(lat);

			// Baseline CO2 (e.g. ~418 ppm on Earth, or dense CO2 on Duna/Eve)
			if (body.bodyName.Contains("Eve") || body.bodyName.Contains("Venus"))
			{
				xco2 = 965000.0; // 96.5% CO2
				xch4 = 20.0;
				aod = 3.5;
				carbonFluxType = "⚠️ 极端超厚二氧化碳温室失控大气";
			}
			else if (body.bodyName.Contains("Duna") || body.bodyName.Contains("Mars"))
			{
				xco2 = 953000.0; // 95.3% CO2
				xch4 = 10.5; // Martian methane mystery
				aod = 0.45;
				carbonFluxType = "稀薄富二氧化碳大气 (极区季节性干冰升华通量)";
			}
			else
			{
				// Earth / Kerbin
				double seasonal = Math.Sin(Planetarium.GetUniversalTime() / (86400 * 365.25) * 2 * Math.PI) * 4.0;
				double latPattern = (absLat < 20.0) ? -2.0 : ((lat > 20.0 && lat < 60.0) ? 6.0 : 0.0);
				double localEmission = (Math.Sin(lat * 0.3) * Math.Cos(lon * 0.25) > 0.6) ? 12.0 : -3.0;

				xco2 = 416.5 + seasonal + latPattern + localEmission;
				xch4 = 1885.0 + localEmission * 6.5;
				aod = Math.Max(0.04, 0.15 + (localEmission > 0 ? 0.25 : 0.0));
				ozoneDU = 300.0 + Math.Cos(latRad(lat)) * 40.0;

				if (localEmission > 8.0)
				{
					carbonFluxType = "🏭 强碳源排放区 (高密度工业区/化石燃料燃烧带)";
				}
				else if (localEmission < -2.0)
				{
					carbonFluxType = "🌲 强碳汇吸收区 (原始森林光合吸收 / 浮游植物碳汇)";
				}
				else
				{
					carbonFluxType = "⚖️ 碳循环平衡区 (全球基线通量)";
				}
			}

			xco2Str = string.Format("{0:F1} ppm", xco2);
			xch4Str = string.Format("{0:F0} ppb", xch4);
			aodStr = string.Format("AOD 550nm: {0:F2}", aod);
			carbonFluxStr = carbonFluxType;
		}

		private static double latRad(double lat) { return lat * Math.PI / 180.0; }

		[KSPEvent(guiActive = true, guiName = "🌱 生成温室气体与碳源碳汇监测简报", groupName = "GreenhouseGas")]
		public void ShowAtmosphereBriefing()
		{
			soundGreenhouseGases();
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine(string.Format("🛰️ 仪器型号: {0} 高光谱温室气体吸收光谱仪", part.partInfo.title));
			sb.AppendLine(string.Format("🌍 目标天体: {0} | 轨道高度: {1:N1} km", vessel.mainBody.bodyName, vessel.altitude / 1000.0));
			sb.AppendLine(string.Format("📍 测线位置: {0:F3}°{1}, {2:F3}°{3}",
				Math.Abs(vessel.latitude), vessel.latitude >= 0 ? "N" : "S",
				Math.Abs(vessel.longitude), vessel.longitude >= 0 ? "E" : "W"));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine("【大气微量温室气体柱浓度 (Column Abundance)】");
			sb.AppendLine(string.Format(" • 二氧化碳柱平均干空气体积分数 (XCO2): {0:F2} ppm", xco2));
			sb.AppendLine(string.Format(" • 甲烷柱平均体积分数 (XCH4):            {0:F1} ppb", xch4));
			sb.AppendLine(string.Format(" • 气溶胶光学厚度 (AOD at 550nm):       {0:F3}", aod));
			sb.AppendLine("【区域碳通量与生态碳汇判识】");
			sb.AppendLine(string.Format(" • 区域碳通量性质结论:                  {0}", carbonFluxType));
			sb.AppendLine(string.Format(" • 臭氧总柱量 (Total Column Ozone):      {0:F1} DU (多布森单位)", ozoneDU));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

			PopupDialog.SpawnPopupDialog(
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new MultiOptionDialog(
					"SCANsatAtmosphereBriefing",
					sb.ToString(),
					"🌱 大气温室气体柱浓度与碳源汇综合监测简报",
					HighLogic.UISkin,
					new DialogGUIButton("关闭", null, true)
				),
				false,
				HighLogic.UISkin
			);
		}
	}
}
