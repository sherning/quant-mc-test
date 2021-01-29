﻿using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Function
{
    public sealed class TradeInfo : FunctionSeries<bool>
    {
        //================================= Project Documentation =================================
        // Project Name : TradeInfo
        // Platform     : Multicharts
        // Class Type   : Function Series Class
        // Version      : v.1.0.0
        // Date         : 24 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Returns      : 
        // Remarks      : To be applied on strategy classes only
        //=========================================================================================

        public TradeInfo(CStudyControl m) : base(m)
        {

        }
        protected override void Create()
        {
        }

        int CurrentPosition;
        protected override void StartCalc()
        {
            // snap short of the current position
            CurrentPosition = StrategyInfo.MarketPosition;

        }
        protected override bool CalcBar()
        {
            throw new NotImplementedException();
        }
    }

}


namespace PowerLanguage.Strategy
{
    public class _SherNing_StrategyFramework : SignalObject
    {
        //================================= Project Documentation =================================
        // Project Name : Strategy Framework
        // Platform     : Multicharts
        // Class Type   : Strategy Class
        // Version      : v.1.0.0
        // Date         : 24 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Framework for strategy function testing
        // Returns      : 
        // Remarks      : 
        //=========================================================================================

        public _SherNing_StrategyFramework(object _ctx) : base(_ctx) { }
        protected override void Create()
        {
        }
        protected override void StartCalc()
        {
        }
        protected override void CalcBar()
        {
        }
    }
}