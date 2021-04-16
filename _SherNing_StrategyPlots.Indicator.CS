using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    [SameAsSymbol(false)]
    public class _SherNing_StrategyPlots : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : Strategy Plots
        // Platform     : Multicharts
        // Class Type   : Indicator
        // Date         : 15 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Plot values from strategy
        // Remarks      : 
        //=========================================================================================


        [Input] public string Plot_1_Settings { get; set; }
        [Input] public int Plot_1_Num { get; set; }
        [Input] public int Plot_1_Width { get; set; }
        [Input] public int Plot_1_Offset { get; set; }
        [Input] public Color Plot_1_Color { get; set; }

        [Input] public string Plot_2_Settings { get; set; }
        [Input] public int Plot_2_Num { get; set; }
        [Input] public int Plot_2_Width { get; set; }
        [Input] public int Plot_2_Offset { get; set; }
        [Input] public Color Plot_2_Color { get; set; }

        [Input] public string Plot_3_Settings { get; set; }
        [Input] public int Plot_3_Num { get; set; }
        [Input] public int Plot_3_Width { get; set; }
        [Input] public int Plot_3_Offset { get; set; }
        [Input] public Color Plot_3_Color { get; set; }

        [Input] public string Plot_4_Settings { get; set; }
        [Input] public int Plot_4_Num { get; set; }
        [Input] public int Plot_4_Width { get; set; }
        [Input] public int Plot_4_Offset { get; set; }
        [Input] public Color Plot_4_Color { get; set; }

        public _SherNing_StrategyPlots(object _ctx) : base(_ctx)
        {
            Plot_1_Settings = "Plot 1 Settings";
            Plot_2_Settings = "Plot 2 Settings";
            Plot_3_Settings = "Plot 3 Settings";
            Plot_4_Settings = "Plot 4 Settings";

            // default plot numbers
            Plot_1_Num = 1;
            Plot_2_Num = 2;
            Plot_3_Num = 3;
            Plot_4_Num = 4;

            Plot_1_Offset = Plot_2_Offset = Plot_3_Offset = Plot_4_Offset = 0;

            // set default colors and width
            Plot_1_Color = Plot_3_Color = Color.DeepPink; // 255,0,128
            Plot_2_Color = Plot_4_Color = Color.FromArgb(128, 128, 255);
            Plot_1_Width = Plot_2_Width = Plot_3_Width = Plot_4_Width = 1;
        }

        const int NumOfPlots = 4;
        List<IPlotObject> PlotList;
        protected override void Create()
        {
            PlotList = new List<IPlotObject>();

            string[] plotNames = new string[]
            {
                "", "Plot 1", "Plot 2", "Plot 3", "Plot 4"
            };

            // avoid using [0]
            for (int i = 0; i <= NumOfPlots; i++)
            {
                PlotList.Add(
                    AddPlot(new PlotAttributes(
                        plotNames[i],
                        EPlotShapes.Line,
                        // default color 
                        Color.White, Color.White,
                        // default width
                        3, EPlotStyle.Solid, true)));
            }
        }
     
        protected override void CalcBar()
        {
            // plot 1
            if (StrategyInfo.GetPlotValue(Plot_1_Num) != 0)
                PlotList[1].Set(Plot_1_Offset, StrategyInfo.GetPlotValue(Plot_1_Num), Plot_1_Color, Plot_1_Width);

            // plot 2
            if (StrategyInfo.GetPlotValue(Plot_2_Num) != 0)
                PlotList[2].Set(Plot_2_Offset, StrategyInfo.GetPlotValue(Plot_2_Num), Plot_2_Color, Plot_2_Width);

            // plot 3
            if (StrategyInfo.GetPlotValue(Plot_3_Num) != 0)
                PlotList[3].Set(Plot_3_Offset, StrategyInfo.GetPlotValue(Plot_3_Num), Plot_3_Color, Plot_3_Width);

            // plot 4
            if (StrategyInfo.GetPlotValue(Plot_4_Num) != 0)
                PlotList[4].Set(Plot_4_Offset, StrategyInfo.GetPlotValue(Plot_4_Num), Plot_4_Color, Plot_4_Width);
        }
    }
}