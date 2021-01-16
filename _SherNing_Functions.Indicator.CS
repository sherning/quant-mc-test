using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using PowerLanguage.Function;

namespace PowerLanguage.Function
{
    public sealed class DataChecker : FunctionSeries<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Data Checker
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 09 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Check for missing days in data source
        // Remarks      : 
        //=========================================================================================

        public DataChecker(CStudyControl m) : base(m)
        {
            //================================= Project Documentation =================================
            // Source : Futures
            // Futures operating hours are weird. 
            // The only holidays for futures are Good Friday, Christmas and New Year
            //=========================================================================================
        }

        List<DateTime> Dates;
        DateTime PrevTradingDay;

        protected override void StartCalc()
        {
            Dates = new List<DateTime>();
            PrevTradingDay = Bars.Time[0];
        }

        protected override string CalcBar()
        {
            DateTime today = Bars.Time[0];

            if (today.Date != PrevTradingDay.Date)
            {
                DateTime prev = PrevTradingDay;
                PrevTradingDay = today;

                if (today.DayOfWeek == DayOfWeek.Monday && prev.DayOfWeek == DayOfWeek.Friday)
                    return "";

                TimeSpan ts = today.Date - prev.Date;
                if (ts.Days < 2) return "";

                for (int i = 1; i < ts.Days; i++)
                {
                    today = today.AddDays(-1);

                    if (today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday)
                        continue;

                    Dates.Add(today);
                }
            }

            return "Check for missing trading day";
        }

        public void PrintDates()
        {
            int year = 0;

            foreach (DateTime date in Dates)
            {
                if (date.Year != year)
                {
                    year = date.Year;
                    Output.WriteLine("");
                }

                Print("Missing Trading Dates: " + date + " Day of week: " + date.DayOfWeek);
            }
        }

        #region Print Functions
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion
    }

    public sealed class Statistics : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Statistics
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 28 Nov 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      :
        //=========================================================================================

        /*/
         *======================================== Version ========================================
         *  28/11/2020 - Statistics
         * 
         *======================================== Version ========================================
        /*/

        public Statistics(CStudyControl m) : base(m) { }

        public double SampleStandardDeviation(List<double> list)
        {
            double mean = Mean(list);

            double variance = 0;

            for (int i = 0; i < list.Count; i++)
                variance += Math.Pow((list[i] - mean), 2);

            // sample std dev is n - 1
            variance /= list.Count - 1;

            return Math.Sqrt(variance);
        }

        public double SampleStandardDeviation(double[] dataSet)
        {
            double mean = Mean(dataSet);

            double variance = 0;

            for (int i = 0; i < dataSet.Length; i++)
                variance += Math.Pow((dataSet[i] - mean), 2);

            // sample std dev is n - 1
            variance /= dataSet.Length - 1;

            return Math.Sqrt(variance);
        }

        public double StandardDeviation(List<double> list)
        {
            double mean = Mean(list);

            double variance = 0;

            for (int i = 0; i < list.Count; i++)
                variance += Math.Pow((list[i] - mean), 2);

            variance /= list.Count;

            return Math.Sqrt(variance);
        }

        public double Mean(List<double> list)
        {
            if (list.Count == 0) return 0;

            double sum = 0;
            for (int i = 0; i < list.Count; i++)
                sum += list[i];

            return sum / list.Count;
        }

        public double Mean(double[] dataSet)
        {
            if (dataSet.Length <= 0) return 0;

            double mean = 0;
            for (int i = 0; i < dataSet.Length; i++)
                mean += dataSet[i];

            return mean / dataSet.Length;
        }

        public double[] RegressionLine(double[,] table)
        {
            if (table.Rank != 2) return null;

            // return y = mx + b, [m,b]
            double[] results = new double[2];

            // table col = rank. two dimension, rank = 2
            int rows = table.Length / table.Rank;

            // get sum(x) is the sum of all x-values
            double sumX = 0;
            for (int i = 0; i < rows; i++)
                sumX += table[i, 0];

            // get sum(y) is the sum of all y-values
            double sumY = 0;
            for (int i = 0; i < rows; i++)
                sumY += table[i, 1];

            // get sum(xy) is the sum of the product of x and y
            double sumXY = 0;
            for (int i = 0; i < rows; i++)
                sumXY += table[i, 0] * table[i, 1];

            // get sum(x^2) is the sum of x^2
            double sumX2 = 0;
            for (int i = 0; i < rows; i++)
                sumX2 += Math.Pow(table[i, 0], 2);

            // calculate output values
            double m = ((rows * sumXY) - (sumX * sumY))
                / ((rows * sumX2) - Math.Pow(sumX, 2));

            double b = (sumY - (m * sumX)) / rows;

            // cache and return output values
            results[0] = m;
            results[1] = b;

            string equation = string.Format("y = {0}x + {1} ", m, b);
            //Print(equation);

            return results;
        }

        public double CorrelationCoefficient(double[,] table)
        {
            if (table.Rank != 2) return 0;

            // table col = rank. two dimension, rank = 2
            int rows = table.Length / table.Rank;

            double xBar = 0;
            for (int i = 0; i < rows; i++)
                xBar += table[i, 0];

            xBar /= rows;

            double yBar = 0;
            for (int i = 0; i < rows; i++)
                yBar += table[i, 1];

            yBar /= rows;

            // cache value for std dev calculation
            double[] xiMinusxBarArr = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                double val = table[i, 0] - xBar;
                xiMinusxBarArr[i] = val;
            }

            double[] yiMinusyBarArr = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                double val = table[i, 1] - yBar;
                yiMinusyBarArr[i] = val;
            }

            double stdDevX = 0;
            for (int i = 0; i < rows; i++)
                stdDevX += Math.Pow(xiMinusxBarArr[i], 2);

            // sample std dev n - 1
            stdDevX = Math.Sqrt(stdDevX / (rows - 1));

            double stdDevY = 0;
            for (int i = 0; i < rows; i++)
                stdDevY += Math.Pow(yiMinusyBarArr[i], 2);

            stdDevY = Math.Sqrt(stdDevY / (rows - 1));

            // calculate correlation r value
            double r = 0;
            for (int i = 0; i < rows; i++)
                r += (xiMinusxBarArr[i] / stdDevX) * (yiMinusyBarArr[i] / stdDevY);

            // 1 / (n - 1)
            r /= (rows - 1);

            return r;
        }

        public double CorrelationCoefficient(double[] set1, double[] set2)
        {
            if (set1.Length != set2.Length) return 0;

            double set1Mean = Mean(set1);
            double set2Mean = Mean(set2);

            int length = set1.Length;

            // cache value for std dev calculation
            double[] xiMinusxBarArr = new double[length];

            for (int i = 0; i < length; i++)
                xiMinusxBarArr[i] = set1[i] - set1Mean;

            double[] yiMinusyBarArr = new double[length];

            for (int i = 0; i < length; i++)
                yiMinusyBarArr[i] = set2[i] - set2Mean;

            double stdDevX = SampleStandardDeviation(set1);
            double stdDevY = SampleStandardDeviation(set2);

            // calculate correlation r value
            double r = 0;
            for (int i = 0; i < length; i++)
                r += (xiMinusxBarArr[i] / stdDevX) * (yiMinusyBarArr[i] / stdDevY);

            // 1 / (n - 1)
            r /= length - 1;

            // provides direction and strength
            // r = Covariance(set1, set2) / (stdDevX * stdDevY))
            return r;
        }

        public double RootMeanSqrError(double[,] table)
        {
            double[] data = RegressionLine(table);

            double m = data[0];
            double b = data[1];

            int dataPoints = table.Length / table.Rank;

            double[] forecast = new double[dataPoints];

            for (int i = 0; i < dataPoints; i++)
                forecast[i] = (m * table[i, 0]) + b;

            double[] residual = new double[dataPoints];
            for (int i = 0; i < dataPoints; i++)
                residual[i] = Math.Pow(table[i, 1] - forecast[i], 2);

            double residualSum = 0;
            for (int i = 0; i < dataPoints; i++)
                residualSum += residual[i];

            double rmse = Math.Sqrt(residualSum / (dataPoints - 1));
            return rmse;
        }

        public double CoefficientOfDetermination(double[,] table)
        {
            // How much or what percentage of the total variation in y is described
            // by the variation in x ?

            // get data from Regression Line
            double[] data = RegressionLine(table);

            double m = data[0];
            double b = data[1];

            int dataPoints = table.Length / table.Rank;

            // corresponding y value for each x, on the regression line
            double[] yValue = new double[dataPoints];
            for (int i = 0; i < dataPoints; i++)
                yValue[i] = (m * table[i, 0]) + b;

            // sqaured distance of the y to the regression line
            double[] sqrError = new double[dataPoints];
            for (int i = 0; i < dataPoints; i++)
                sqrError[i] = Math.Pow(table[i, 1] - yValue[i], 2);

            // y mean
            double yMean = 0;
            for (int i = 0; i < dataPoints; i++)
                yMean += table[i, 1];

            yMean /= dataPoints;

            // y - yMean
            double[] sqrDistanceFromMean = new double[dataPoints];
            for (int i = 0; i < dataPoints; i++)
                sqrDistanceFromMean[i] = Math.Pow(table[i, 1] - yMean, 2);

            // totals 
            double totalSqrError = 0;
            for (int i = 0; i < dataPoints; i++)
                totalSqrError += sqrError[i];

            double totalSqrDistanceFromMean = 0;
            for (int i = 0; i < dataPoints; i++)
                totalSqrDistanceFromMean += sqrDistanceFromMean[i];

            // % of total variation which is explained by regression line.
            return 1 - (totalSqrError / totalSqrDistanceFromMean);
        }

        public double ChiSquareTest(double[] observed, double[] expected)
        {
            // the data is the actual quantity, and not the proportion

            // check if arrays are of equal lengths
            if (observed.Length != expected.Length) return 0;

            double chiStatistic = 0;
            for (int i = 0; i < observed.Length; i++)
                chiStatistic +=
                    Math.Pow(observed[i] - expected[i], 2) / expected[i];

            return chiStatistic;
        }

        public double ChiSquareContigency(double[] firstSet, double[] secondSet)
        {
            // check
            if (firstSet.Length != secondSet.Length) return 0;

            double set1Total = 0;
            for (int i = 0; i < firstSet.Length; i++)
                set1Total += firstSet[i];

            double set2Total = 0;
            for (int i = 0; i < secondSet.Length; i++)
                set2Total += secondSet[i];

            double[] subTotal = new double[firstSet.Length];

            for (int i = 0; i < firstSet.Length; i++)
                subTotal[i] = firstSet[i] + secondSet[i];

            double total = set1Total + set2Total;

            double firstSetPercent = set1Total / total;
            double secondSetPercent = set2Total / total;

            double[] expected = new double[firstSet.Length * 2];
            double[] observed = new double[firstSet.Length * 2];

            for (int i = 0; i < subTotal.Length; i++)
            {
                expected[i] = subTotal[i] * firstSetPercent;
                expected[i + subTotal.Length] = subTotal[i] * secondSetPercent;
            }

            for (int i = 0; i < subTotal.Length; i++)
            {
                observed[i] = firstSet[i];
                observed[i + subTotal.Length] = secondSet[i];
            }

            // (rows - 1) * ( cols - 1)
            int degreesOfFreedom = (2 - 1) * (firstSet.Length - 1);

            return ChiSquareTest(observed, expected);
        }

        public double Covariance(double[] set1, double[] set2)
        {
            if (set1.Length != set2.Length) return 0;

            int length = set1.Length;

            double set1Mean = Mean(set1);
            double set2Mean = Mean(set2);

            double covariance = 0;
            for (int i = 0; i < length; i++)
                covariance += (set1[i] - set1Mean) * (set2[i] - set2Mean);

            // Sample => n - 1
            covariance /= length - 1;

            // covariance only measures the direction and not strength
            return covariance;
        }

        public double[] GetArray(params double[] dataset)
        {
            if (dataset.Length <= 0) return null;

            double[] arr = new double[dataset.Length];

            for (int i = 0; i < dataset.Length; i++)
                arr[i] = dataset[i];

            return arr;
        }

        protected override string CalcBar()
        {
            return "Does not return any value";
        }

        #region Print Functions
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion

    }

    public sealed class HullMovingAverage : FunctionSeries<double>
    {
        //================================= Project Documentation =================================
        // Project Name : Hull Moving Average
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 08 Dec 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      :
        //=========================================================================================

        public int Length { get; set; }
        public HullMovingAverage(CStudyControl m) : base(m) { }
        public HullMovingAverage(CStudyControl m, bool auto) : base(m) { Auto = auto; }


        // fields
        int[] LengthArr;
        List<double>[] PriceLists;
        bool Auto;

        protected override void StartCalc()
        {
            if (Length < 1) Length = 1;

            // Length[1], Length[2], Length[3]
            LengthArr = new int[4];

            // calculate only when there is a change in _Length value 
            int halvedLength;

            if ((Math.Ceiling((double)(Length / 2)) - (Length / 2)) <= 0.5)
                halvedLength = (int)Math.Ceiling((double)(Length / 2));
            else
                halvedLength = (int)Math.Floor((double)(Length / 2));

            int sqrRootLength;
            double sqLength = Math.Sqrt(Length);

            if ((Math.Ceiling(sqLength) - sqLength) <= 0.5)
                sqrRootLength = (int)Math.Ceiling(sqLength);
            else
                sqrRootLength = (int)Math.Floor(sqLength);

            //Lengths[0] = 0 by default
            LengthArr[1] = halvedLength;
            LengthArr[2] = Length;
            LengthArr[3] = sqrRootLength;

            // create an array of 4 price list.
            PriceLists = new List<double>[4];

            //PriceLists[0] = null;
            PriceLists[1] = new List<double>();
            PriceLists[2] = new List<double>();
            PriceLists[3] = new List<double>();
        }

        public void AddData(double data)
        {
            // using insert method, i got to take caution.
            PriceLists[1].Insert(0, data);
            PriceLists[2].Insert(0, data);

            // clean up list for unwanted values
            if (PriceLists[1].Count > LengthArr[1])
                PriceLists[1].RemoveAt(LengthArr[1]);

            if (PriceLists[2].Count > LengthArr[2])
                PriceLists[2].RemoveAt(LengthArr[2]);
        }
        protected override double CalcBar()
        {
            if (Auto) AddData(Bars.CloseValue);

            // insufficient data
            if (PriceLists[2].Count < Length) return -1;

            // calculate 
            double wma1 = 2.0 * WeightedAverage(PriceLists[1], LengthArr[1]);
            double wma2 = WeightedAverage(PriceLists[2], LengthArr[2]);

            // add the calculated data to pricelist 3
            PriceLists[3].Insert(0, wma1 - wma2);

            // check for sufficient data
            if (PriceLists[3].Count > LengthArr[3])
            {
                // clean up list for unwanted values
                PriceLists[3].RemoveAt(LengthArr[3]);

                // calculate the weighted average and return.
                return WeightedAverage(PriceLists[3], LengthArr[3]);
            }

            return -1;
        }

        private double WeightedAverage(List<double> dataSet, int length)
        {
            // the recent data is more heavily weighted
            if (length < 1) return 0;

            double denominator = 1 / ((length + 1) * length * 0.5);
            double weightedSum = 0;

            // newest data is front loaded.
            for (int i = 0; i < length; i++)
                weightedSum += (length - i) * dataSet[i];

            return weightedSum * denominator;
        }
    }

    public sealed class PriceActionLineF : FunctionSeries<double>
    {
        //================================= Project Documentation =================================
        // Project Name : Price Action Line Single Timeframe
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 08 Dec 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      :
        //=========================================================================================

        public int Length { get; set; }
        public int Smooth { get; set; }
        public ISeries<double> Price { get; set; }

        public PriceActionLineF(CStudyControl m) : base(m) { }

        HullMovingAverage HMA;
        protected override void Create()
        {
            HMA = new HullMovingAverage(this);
        }

        double Alpha, Gain;
        protected override void StartCalc()
        {
            if (Length < 1) Length = 1;
            if (Gain < 0) Gain = 0;

            // smoothing constant
            Alpha = 2.0 / (Length + 1.0);
            Gain = Smooth / 100.0;

            // HMA Properties
            HMA.Length = Length;
        }

        protected override double CalcBar()
        {
            if (Bars.Status == EBarState.Close)
            {
                double price = Price.Value;
                HMA.AddData(price);

                // cache
                double hmaValue = HMA.Value;

                if (price - this[1] > 0)
                    // -ve gain
                    return Alpha * (hmaValue + (-Gain) * (hmaValue - this[1])) + (1.0 - Alpha) * this[1];
                else if (price - this[1] < 0)
                    // +ve gain
                    return Alpha * (hmaValue + (Gain) * (hmaValue - this[1])) + (1.0 - Alpha) * this[1];
                else
                    // zero gain
                    return Alpha * (hmaValue + (0.0) * (hmaValue - this[1])) + (1.0 - Alpha) * this[1];
            }

            return -1;
        }
    }

    public sealed class PriceActionLineMTF : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Price Action Line Multitime Frame
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 28 Nov 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Daily, Weekly, Monthly
        // Remarks      : 
        //=========================================================================================

        public int Length { get; set; }
        public int Smooth { get; set; }

        // function output
        public VariableSeries<double> PAL_1 { get { return PAL[1]; } }
        public int TrendDir_1 { get { return TrendDirection[1][0]; } }
        public VariableSeries<double> PAL_2 { get { return PAL[2]; } }
        public int TrendDir_2 { get { return TrendDirection[2][0]; } }
        public VariableSeries<double> PAL_3 { get { return PAL[3]; } }
        public int TrendDir_3 { get { return TrendDirection[3][0]; } }


        public PriceActionLineMTF(CStudyControl m) : base(m) { }

        // 4 timeframes, day, week, month, year, avoid [0] for ambiguity
        const int TFs = 5;

        // function series, works as variable series, which has built in list
        ClosingTick[] ClosingTicks;
        HullMovingAverage[] HMA;
        VariableSeries<double>[] PAL;
        VariableSeries<bool>[] CloseTick;
        VariableSeries<int>[] TrendDirection;
        protected override void Create()
        {
            ClosingTicks = new ClosingTick[TFs];
            CloseTick = new VariableSeries<bool>[TFs];
            HMA = new HullMovingAverage[TFs];
            PAL = new VariableSeries<double>[TFs];
            TrendDirection = new VariableSeries<int>[TFs];

            for (int timeframe = 0; timeframe < TFs; timeframe++)
            {
                ClosingTicks[timeframe] = new ClosingTick(this);
                HMA[timeframe] = new HullMovingAverage(this);
                PAL[timeframe] = new VariableSeries<double>(this);
                CloseTick[timeframe] = new VariableSeries<bool>(this);
                TrendDirection[timeframe] = new VariableSeries<int>(this);
            }
        }

        double Alpha, Gain;
        double[] BarCount, Highs, Lows, Opens, Closes, Prices;
        bool CurrTick, PrevTick;
        protected override void StartCalc()
        {
            if (Length < 1) Length = 1;

            Alpha = 2.0 / (Length + 1.0);
            Gain = Smooth / 100.0;

            BarCount = new double[TFs];
            Highs = new double[TFs];
            Lows = new double[TFs];
            Opens = new double[TFs];
            Closes = new double[TFs];
            Prices = new double[TFs];

            CurrTick = PrevTick = false;

            // properties
            for (int i = 0; i < TFs; i++)
                HMA[i].Length = Length;

            GetTimeFramesForCalc();
        }

        private void GetTimeFramesForCalc()
        {
            EResolution resolution = Bars.Info.Resolution.Type;
            int resSize = (int)Bars.Info.Resolution.Size;

            // base chart in minutes
            if (resolution == EResolution.Minute)
            {
                for (int i = 0; i < TFs; i++)
                    ClosingTicks[i].Resolution = EResolution.Minute;

                ClosingTicks[TFs - 1].Resolution = EResolution.Day;

                switch (resSize)
                {
                    default:
                    case 0:
                        break;

                    case 5:
                        ClosingTicks[1].ResolutionSize = 5;
                        ClosingTicks[2].ResolutionSize = 60;
                        ClosingTicks[3].ResolutionSize = 240;
                        break;

                    case 10:
                        ClosingTicks[1].ResolutionSize = 10;
                        ClosingTicks[2].ResolutionSize = 60;
                        ClosingTicks[3].ResolutionSize = 240;
                        break;

                    case 15:
                        ClosingTicks[1].ResolutionSize = 15;
                        ClosingTicks[2].ResolutionSize = 60;
                        ClosingTicks[3].ResolutionSize = 240;
                        break;

                    case 20:
                        ClosingTicks[1].ResolutionSize = 20;
                        ClosingTicks[2].ResolutionSize = 60;
                        ClosingTicks[3].ResolutionSize = 240;
                        break;

                    case 30:
                        ClosingTicks[1].ResolutionSize = 30;
                        ClosingTicks[2].ResolutionSize = 60;
                        ClosingTicks[3].ResolutionSize = 240;
                        break;

                    case 60:
                        ClosingTicks[1].ResolutionSize = 60;
                        ClosingTicks[2].ResolutionSize = 240;
                        ClosingTicks[3].Resolution = EResolution.Day;
                        ClosingTicks[4].Resolution = EResolution.Week;
                        break;

                    case 240:
                        ClosingTicks[1].ResolutionSize = 240;
                        ClosingTicks[2].Resolution = EResolution.Day;
                        ClosingTicks[3].Resolution = EResolution.Week;
                        ClosingTicks[4].Resolution = EResolution.Month;
                        break;
                }
            }

            // base chart in day
            else if (resolution == EResolution.Day)
            {
                // day, week, month, year
                for (int i = 0; i < TFs; i++)
                    ClosingTicks[i].Resolution = (EResolution)(3 + i);
            }
        }

        protected override string CalcBar()
        {
            if (Bars.Status != EBarState.Close) return "Calculate on bar close only";

            for (int timeframe = 1; timeframe < TFs; timeframe++)
            {
                // optimization, cache variableseries
                CurrTick = CloseTick[timeframe].Value = ClosingTicks[timeframe][0];
                PrevTick = CloseTick[timeframe][1];

                // for each timeframe do the following
                BuildMtfData(timeframe);
                SetCalcPrice(timeframe);
                CalcPalValue(timeframe);
                SetTrendDirection(timeframe);

                if (CurrTick)
                    BarCount[timeframe]++;
            }

            return "No output";
        }

        private void SetTrendDirection(int timeframe)
        {
            if (CurrTick)
            {
                if (PAL[timeframe][0] >= PAL[timeframe][1])
                {
                    TrendDirection[timeframe].Value = 1;
                }
                else
                {
                    TrendDirection[timeframe].Value = -1;
                }
            }
        }

        private void CalcPalValue(int timeframe)
        {
            if (CurrTick)
            {
                HMA[timeframe].AddData(Prices[timeframe]);

                double hma = HMA[timeframe].Value;

                if (Prices[timeframe] - PAL[timeframe][1] > 0)
                {
                    // Gain < 0
                    PAL[timeframe].Value
                        = Alpha * (hma + (-Gain) * (hma - PAL[timeframe][1]))
                        + (1.0 - Alpha) * PAL[timeframe][1];
                }
                else if (Prices[timeframe] - PAL[timeframe][1] < 0)
                {
                    // Gain > 0
                    PAL[timeframe].Value
                       = Alpha * (hma + Gain * (hma - PAL[timeframe][1]))
                       + (1.0 - Alpha) * PAL[timeframe][1];
                }
                else
                {
                    // Gain = 0
                    PAL[timeframe].Value
                       = Alpha * (hma + 0.0 * (hma - PAL[timeframe][1]))
                       + (1.0 - Alpha) * PAL[timeframe][1];
                }
            }
        }

        private void SetCalcPrice(int timeframe)
        {
            if (CurrTick)
            {
                // filter price type here
                Prices[timeframe] = Closes[timeframe];
            }
        }

        private void BuildMtfData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                if (PrevTick)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                    Closes[timeframe] = Bars.CloseValue;
                }
                else
                {
                    Highs[timeframe] = Math.Max(Bars.HighValue, Highs[timeframe]);
                    Lows[timeframe] = Math.Min(Bars.LowValue, Lows[timeframe]);
                }

                if (CurrTick)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }

        #region Print
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion

    }

    public sealed class USBankHolidays : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : US Bank Holidays
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 06 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Determine US Bank Holidays for current year
        // Remarks      : 
        //=========================================================================================


        public USBankHolidays(CStudyControl m) : base(m) { }

        public Dictionary<DateTime, string> GetUSHolidays(int year)
        {
            ESymbolCategory catergory = Bars.Info.Category;
            Dictionary<DateTime, string> bankHolidays = new Dictionary<DateTime, string>();
            USHoliday day;

            if (catergory == ESymbolCategory.Stock)
            {
                day = NewYearDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = MartinLutherDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = PresidentDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = GoodFriday(year);
                bankHolidays.Add(day.Date, day.Name);

                day = MemorialDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = IndependenceDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = LabourDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = ThanksGivingDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = ChristmasDay(year);
                bankHolidays.Add(day.Date, day.Name);

            }
            else if (catergory == ESymbolCategory.Forex || catergory == ESymbolCategory.Cash)
            {
                day = NewYearDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = ChristmasDay(year);
                bankHolidays.Add(day.Date, day.Name);
            }
            else if (catergory == ESymbolCategory.Future)
            {
                day = NewYearDay(year);
                bankHolidays.Add(day.Date, day.Name);

                day = GoodFriday(year);
                bankHolidays.Add(day.Date, day.Name);

                day = ChristmasDay(year);
                bankHolidays.Add(day.Date, day.Name);
            }

            return bankHolidays;
        }

        public DateTime FirstTradingDayOfYear(int year)
        {
            // start from 2nd Jan
            DateTime day = new DateTime(year, 1, 2);

            for (int i = 0; i < 8; i++)
            {
                if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday) return day;
                day = day.AddDays(1);
            }

            return day;
        }

        private USHoliday NewYearDay(int year)
        {
            return new USHoliday("New Year's Day", new DateTime(year, 1, 1));
        }

        private USHoliday MartinLutherDay(int year)
        {
            DateTime day = new DateTime(year, 1, 15);

            while (day.DayOfWeek != DayOfWeek.Monday)
                day = day.AddDays(1);

            return new USHoliday("Martin Luther King Jr Day", day);
        }

        private USHoliday PresidentDay(int year)
        {
            // third monday of Feb
            DateTime day = new DateTime(year, 2, 1);

            while (day.DayOfWeek != DayOfWeek.Monday)
                day = day.AddDays(1);

            day = day.AddDays(14);

            return new USHoliday("President's Day", day);
        }

        private USHoliday GoodFriday(int year)
        {
            int day, month;

            int g = year % 19;
            int c = year / 100;
            int h = (c - (int)(c / 4) - (int)((8 * c + 13) / 25) + 19 * g + 15) % 30;
            int i = h - (int)(h / 28) * (1 - (int)(h / 28) * (int)(29 / (h + 1)) * (int)((21 - g) / 11));

            day = i - ((year + (int)(year / 4) + i + 2 - c + (int)(c / 4)) % 7) + 28;
            month = 3;

            if (day > 31)
            {
                month++;
                day -= 31;
            }

            // get easter sunday, -2 days for good friday
            DateTime easter = new DateTime(year, month, day);

            easter = easter.AddDays(-2);
            return new USHoliday("Good Friday", easter);
        }

        private USHoliday MemorialDay(int year)
        {
            DateTime day = new DateTime(year, 5, 31);

            while (day.DayOfWeek != DayOfWeek.Monday)
                day = day.AddDays(-1);

            return new USHoliday("Memorial's Day", day);
        }

        private USHoliday IndependenceDay(int year)
        {
            DateTime day = new DateTime(year, 7, 4);

            if (day.DayOfWeek == DayOfWeek.Sunday)
                day = day.AddDays(1);
            else if (day.DayOfWeek == DayOfWeek.Saturday)
                day = day.AddDays(-1);

            return new USHoliday("Independence's Day", day);
        }

        private USHoliday LabourDay(int year)
        {
            DateTime day = new DateTime(year, 9, 1);

            while (day.DayOfWeek != DayOfWeek.Monday)
                day = day.AddDays(1);

            return new USHoliday("Labour's Day", day);
        }

        private USHoliday ThanksGivingDay(int year)
        {
            DateTime day = new DateTime(year, 11, 1);

            while (day.DayOfWeek != DayOfWeek.Thursday)
                day = day.AddDays(1);

            day = day.AddDays(21);

            return new USHoliday("Thanks Giving's Day", day);
        }

        private USHoliday ChristmasDay(int year)
        {
            DateTime day = new DateTime(year, 12, 25);

            if (day.DayOfWeek == DayOfWeek.Saturday) day = day.AddDays(-1);
            else if (day.DayOfWeek == DayOfWeek.Sunday) day = day.AddDays(1);

            return new USHoliday("Christmas's Day", day);
        }

        protected override string CalcBar()
        {
            return "Does not have a return value";
        }

        class USHoliday
        {
            public string Name { get; set; }
            public DateTime Date { get; set; }
            public USHoliday(string name, DateTime date)
            {
                Name = name;
                Date = date;
            }

            public USHoliday() { }
        }
    }

    public sealed class ClosingTick : FunctionSeries<bool>
    {
        //================================= Project Documentation =================================
        // Project Name : Closing Ticks
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 30 Dec 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Determine the closing tick for a given timeframe
        // Remarks      : 
        //=========================================================================================

        public EResolution Resolution { get; set; }
        public int ResolutionSize { get; set; }
        public ClosingTick(CStudyControl m) : base(m) { }

        // reference variables
        USBankHolidays BankHolidays;
        Dictionary<DateTime, string> USHolidays;
        List<DateTime> SpecialFutureCases;
        protected override void Create()
        {
            BankHolidays = new USBankHolidays(this);
            USHolidays = new Dictionary<DateTime, string>();
            SpecialFutureCases = new List<DateTime>();
        }

        // variables
        int Year;
        protected override void StartCalc()
        {
            Year = 0;

            // special session end timing
            SpecialFutureCases.Add(new DateTime(2020, 11, 27, 13, 0, 0));
            SpecialFutureCases.Add(new DateTime(2020, 12, 24, 13, 0, 0));
        }

        protected override bool CalcBar()
        {
            if (Bars.Time[0].Date == BankHolidays.FirstTradingDayOfYear(Bars.Time[0].Year).Date)
            {
                // calculate once annually on the first trading day closing
                Year = Bars.Time[0].Year;
                USHolidays = BankHolidays.GetUSHolidays(Year);
            }

            bool closingTick = false;

            // resolution filter
            switch (Resolution)
            {
                case EResolution.Minute:
                    closingTick = MinutesClosingTick();
                    break;

                case EResolution.Day:
                    closingTick = IsSessionEnd();
                    break;

                case EResolution.Week:
                    closingTick = WeeklyClosingTick();
                    break;

                case EResolution.Month:
                    closingTick = MonthlyClosingTick();
                    break;

                case EResolution.Year:
                    closingTick = AnnualClosingTick();
                    break;

                default:
                    break;
            }

            return closingTick;
        }

        #region Resolution Filter
        private bool AnnualClosingTick()
        {
            if (Bars.Status != EBarState.Close) return false;

            // get last day of the month
            int year = Bars.Time[0].Year;
            int month = 12;
            int day = DateTime.DaysInMonth(year, month);

            DateTime lastTradingDay = new DateTime();

            bool sessionEnd = IsSessionEnd();

            while (day > 0)
            {
                DateTime current = new DateTime(year, month, day);
                if (current.DayOfWeek < DayOfWeek.Saturday &&
                   current.DayOfWeek > DayOfWeek.Sunday &&
                   !USHolidays.ContainsKey(current) &&
                   sessionEnd)
                {
                    lastTradingDay = current;
                    break;
                }

                day--;
            }

            return Bars.Time[0].Date == lastTradingDay.Date;
        }

        private bool MonthlyClosingTick()
        {
            if (Bars.Status != EBarState.Close) return false;

            // get last day of the month
            int year = Bars.Time[0].Year;
            int month = Bars.Time[0].Month;
            int day = DateTime.DaysInMonth(year, month);

            DateTime lastTradingDay = new DateTime();

            bool sessionEnd = IsSessionEnd();

            while (day > 0)
            {
                DateTime current = new DateTime(year, month, day);
                if (current.DayOfWeek < DayOfWeek.Saturday &&
                   current.DayOfWeek > DayOfWeek.Sunday &&
                   !USHolidays.ContainsKey(current) &&
                   sessionEnd)
                {
                    lastTradingDay = current;
                    break;
                }

                day--;
            }

            return Bars.Time[0].Date == lastTradingDay.Date;
        }

        private bool WeeklyClosingTick()
        {
            if (Bars.Status != EBarState.Close) return false;

            // datetime immutable
            DateTime today = Bars.Time[0];
            DateTime tomorrow = today.AddDays(1);

            bool sessionEnd = IsSessionEnd();

            // tomorrow is a friday and a bank holiday
            foreach (var holiday in USHolidays)
            {
                if (tomorrow.Date == holiday.Key.Date &&
                    tomorrow.DayOfWeek == DayOfWeek.Friday &&
                    sessionEnd) return true;
            }

            // tomorrow is sat
            if (tomorrow.DayOfWeek == DayOfWeek.Saturday &&
                sessionEnd) return true;

            // today is 31 Dec and 1st is a Friday
            if (today.Date == new DateTime(Bars.Time[0].Year, 12, 31) &&
                tomorrow.DayOfWeek == DayOfWeek.Friday &&
                sessionEnd)
                return true;

            // special cases
            if (Bars.Info.Category == ESymbolCategory.Future &&
                SpecialFutureCases.Contains(Bars.Time[0]))
                return true;


            return false;
        }

        private bool MinutesClosingTick()
        {
            bool closingTick = false;

            int timeFromOpenTotalMinuate = GetTimeFromSessionOpen();
            int currentTime = ConvertTimeToInt(Bars.TimeValue);
            int sessionEnd = ConvertTimeToInt(Bars.Sessions[0].EndTime);

            bool sessionEnded = currentTime == sessionEnd;

            switch (ResolutionSize)
            {
                default:
                case 0:
                    break;

                case 5:
                    closingTick = timeFromOpenTotalMinuate % 005 == 0 || sessionEnded;
                    break;

                case 10:
                    closingTick = timeFromOpenTotalMinuate % 010 == 0 || sessionEnded;
                    break;

                case 15:
                    closingTick = timeFromOpenTotalMinuate % 015 == 0 || sessionEnded;
                    break;

                case 20:
                    closingTick = timeFromOpenTotalMinuate % 020 == 0 || sessionEnded;
                    break;

                case 30:
                    closingTick = timeFromOpenTotalMinuate % 030 == 0 || sessionEnded;
                    break;

                case 60:
                    closingTick = timeFromOpenTotalMinuate % 060 == 0 || sessionEnded;
                    break;

                case 120:
                    closingTick = timeFromOpenTotalMinuate % 120 == 0 || sessionEnded;
                    break;

                case 240:
                    closingTick = timeFromOpenTotalMinuate % 240 == 0 || sessionEnded;
                    break;
            }

            return closingTick;
        }
        #endregion

        #region Helper
        private bool IsSessionEnd()
        {
            int current = ConvertTimeToInt(Bars.TimeValue);
            int sessionEndTime = ConvertTimeToInt(Bars.Sessions[0].EndTime);

            return current == sessionEndTime;
        }

        private int GetTimeFromSessionOpen()
        {
            // time elapsed in minutes since session open
            int minutesFromOpen = 0;

            // current time from midnight 0000
            int current = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // time at midnight in minutes
            int midnight = 24 * 60;

            int sessionStart = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessionEnd = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            if (current >= sessionStart && current < midnight)
            {
                minutesFromOpen = current - sessionStart;
            }
            else if (current > 0 && current <= sessionEnd)
            {
                minutesFromOpen = midnight - sessionStart + current;
            }
            else if (current == 0)
            {
                minutesFromOpen = midnight - sessionStart;
            }

            return minutesFromOpen;
        }

        private int ConvertTimeToInt(TimeSpan time)
        {
            int hours = time.Hours * 100;
            int minutes = time.Minutes;
            return hours + minutes;
        }

        private int ConvertTimeToInt(DateTime time)
        {
            int hours = time.Hour * 100;
            int minutes = time.Minute;
            return hours + minutes;
        }
        #endregion

        #region Print
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion

    }

    public sealed class TrendTemplateCriteria : FunctionSeries<int>
    {
        //================================= Project Documentation =================================
        // Project Name : Closing Ticks
        // Platform     : Multicharts
        // Class Type   : Function
        // Chart Type   : All
        // Date         : 30 Dec 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        // Version      : v.1.0.0
        //=========================================================================================
        // References   : Mark Minervini stock selection criteria      
        // Obectives    : 
        // Remarks      : returns 8 if all criteria met
        //=========================================================================================

        // output
        public int Condition_1 { get { return Condition1(); } }
        public int Condition_2 { get { return Condition2(); } }
        public int Condition_3 { get { return Condition3(); } }
        public int Condition_4 { get { return Condition4(); } }
        public int Condition_5 { get { return Condition5(); } }
        public int Condition_6 { get { return Condition6(); } }
        public int Condition_7 { get { return Condition7(); } }


        public TrendTemplateCriteria(CStudyControl m) : base(m) { }

        const int NumAvgs = 4;
        VariableSeries<double> Price;
        VariableSeries<double>[] MA;
        protected override void Create()
        {
            Price = new VariableSeries<double>(this);
            MA = new VariableSeries<double>[NumAvgs];

            for (int i = 1; i < NumAvgs; i++)
                MA[i] = new VariableSeries<double>(this);
        }

        protected override int CalcBar()
        {
            Price.Value = Bars.Close[0];

            MA[1].Value = MovingAverage(50);
            MA[2].Value = MovingAverage(150);
            MA[3].Value = MovingAverage(200);

            int ret = Condition1() + Condition2() + Condition3() + Condition4()
                + Condition5() + Condition6() + Condition7();

            return ret;
        }

        #region Conditions
        private int Condition7()
        {
            // Price is trading above the 50 day MA coming out from base
            if (Price[0] > MA[1][0])
                return 1;

            return 0;
        }

        private int Condition6()
        {
            // current price at least 25% from 52 weeks high
            double high = HighestHigh(52 * 5);

            high *= 0.75;

            if (Price[0] > high) return 1;

            return 0;
        }

        private int Condition5()
        {
            // current stock price is at least 25% above 52 weeks low
            // 5 trading days a week, 52 weeks X 5
            double low = LowestLow(52 * 5);

            // 25% from lowest low
            low *= 1.25;

            if (Price[0] > low) return 1;

            return 0;
        }

        private int Condition4()
        {
            // 50 day ma > 200 && > 150
            if (MA[1][0] > MA[2][0] && MA[1][0] > MA[3][0])
                return 1;

            return 0;
        }

        private int Condition3()
        {
            // 200 day MA is trending up for at 
            // least 1 month (20 trading days)
            if (MA[3][0] - MA[3][20] > 0)
                return 1;

            return 0;
        }

        private int Condition2()
        {
            // 150 day moving average > 200 day MA
            if (MA[2].Value > MA[3][0])
                return 1;

            return 0;
        }

        private int Condition1()
        {
            // stock price is above both 150 and 200 day moving avg
            if (Price[0] > MA[2][0] && Price[0] > MA[3][0])
                return 1;

            return 0;
        }
        #endregion

        #region Helper
        private double MovingAverage(int length)
        {
            double sum = 0;

            for (int i = 0; i < length; i++)
                sum += Bars.Close[i];

            return sum / length;
        }

        private double LowestLow(int length)
        {
            double low = double.MaxValue;

            for (int i = 0; i < length; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low;
        }

        private double HighestHigh(int length)
        {
            double high = 0;

            for (int i = 0; i < length; i++)
                high = Math.Max(high, Bars.High[i]);

            return high;
        }
        #endregion

        #region Print Functions
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion

    }

    public sealed class VolumeNormalised : FunctionSeries<double>
    {
        bool DebugMode = true;

        //================================= Project Documentation =================================
        // Project Name : Volume Normalised
        // Platform     : Multicharts
        // Class Type   : Function
        // Chart Type   : Daily charts with volume information
        // Date         : 13 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        // Version      : v.1.0.0
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      : Check the number of bars required for calculation
        //=========================================================================================

        public int Length { get; set; }
        public int CalcMode { get; set; }
        public VolumeNormalised(CStudyControl m) : base(m) { }

        List<double>[] DayOfWeekVolume;
        ClosingTick BarClosingTick;
        protected override void Create()
        {
            // mon to fri, skip [0]
            DayOfWeekVolume = new List<double>[6];
            BarClosingTick = new ClosingTick(this);
        }

        int BarsRequired;
        protected override void StartCalc()
        {
            if (Length < 1) Length = 1;

            for (int i = 0; i <= 5; i++)
                DayOfWeekVolume[i] = new List<double>(Length);

            // designed for daily charts
            BarClosingTick.Resolution = Bars.Info.Resolution.Type;
            BarClosingTick.ResolutionSize = (int)Bars.Info.Resolution.Size;

            // there is no reference for bars back, therefore, maxbarsback = 0
            BarsRequired = Length * 6;
        }
        protected override double CalcBar()
        {
            // calculate end of bar
            if (Bars.Status != EBarState.Close) return 0;

            DateTime today = Bars.Time[0];
            double vol = Bars.Volume[0];
            double ret = 0;

            if (BarClosingTick.Value)
            {
                if (CalcMode == 2)
                {
                    // caculate using day of week average
                    switch (today.DayOfWeek)
                    {
                        default:
                        case DayOfWeek.Saturday:
                        case DayOfWeek.Sunday:
                            break;

                        case DayOfWeek.Monday:
                            {
                                AddData(DayOfWeekVolume[1], vol);
                                ret = CalcData(DayOfWeekVolume[1]);
                                break;
                            }
                        case DayOfWeek.Tuesday:
                            {
                                AddData(DayOfWeekVolume[2], vol);
                                ret = CalcData(DayOfWeekVolume[2]);
                                break;
                            }
                        case DayOfWeek.Wednesday:
                            {
                                AddData(DayOfWeekVolume[3], vol);
                                ret = CalcData(DayOfWeekVolume[3]);
                                break;
                            }
                        case DayOfWeek.Thursday:
                            {
                                AddData(DayOfWeekVolume[4], vol);
                                ret = CalcData(DayOfWeekVolume[4]);
                                break;
                            }
                        case DayOfWeek.Friday:
                            {
                                AddData(DayOfWeekVolume[5], vol);
                                ret = CalcData(DayOfWeekVolume[5]);
                                break;
                            }
                    }

                }
                else if (CalcMode == 1)
                {
                    ret = vol / Average();
                }
            }

            return ret != -1 ? ret : 0;
        }

        #region Calculations
        private double Average()
        {
            double ret = 0;
            for (int i = 1; i <= Length; i++)
                ret += Bars.Volume[Length];

            return ret / Length;
        }
        private double CalcData(List<double> list)
        {
            if (list.Count >= Length && Bars.CurrentBar >= BarsRequired)
            {
                double vol = 0;

                // exclude current day's volume
                for (int i = 0; i < Length; i++)
                    vol += list[i];

                // average volume
                vol /= Length;

                // curr volume / mean of past volume
                return list[Length] / vol;
            }

            return -1;
        }
        private void AddData(List<double> list, double vol)
        {
            if (list.Count > Length)
                list.RemoveAt(0);

            list.Add(vol);
        }

        #endregion

        #region Print Functions
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion
    }

    public sealed class AutoPositionSizing : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Auto Position Sizing
        // Platform     : Multicharts
        // Class Type   : Function Simple
        // Chart Type   : Any chart type
        // Date         : 16 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        // Version      : v.1.0.0
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Returns      : Maximum contracts for a given risk 
        // Remarks      : For forex, update quote manager minimum move to 1
        //=========================================================================================

        /// <summary>
        /// Account Size, e.g. 100,000 = $100,000
        /// </summary>
        public double AccountSize { get; set; }
        /// <summary>
        /// Risk in percentage, e.g. 2 = 2%
        /// </summary>
        public double Risk { get; set; }
        public AutoPositionSizing(CStudyControl m) : base(m) { }

        Dictionary<string, double> PipValues;
        protected override void Create()
        {
            PipValues = new Dictionary<string, double>();
        }

        ESymbolCategory Category;
        string BaseSymbol;
        int MinimumContracts, MaximumContracts, StepSize;
        double DollarRisk;

        protected override void StartCalc()
        {
            // Bars first accessible only in StartCalc()
            Category = Bars.Info.Category;

            if (Category == ESymbolCategory.Cash || Category == ESymbolCategory.Forex)
            {
                GetPipValues();
                MinimumContracts = 20000;
                MaximumContracts = 10000000;
            }
            else if (Category == ESymbolCategory.Stock)
            {
                MinimumContracts = 10;
                MaximumContracts = 10000;
            }
            else if (Category == ESymbolCategory.Future)
            {
                // incomplete
                MinimumContracts = 1;
                MaximumContracts = 1000;

                Print("Info: " + GetTickValue(1));
            }

            StepSize = MinimumContracts;
            DollarRisk = AccountSize * Risk * 0.01;
        }

        protected override string CalcBar()
        {
            return "Calculates the optimal position size for current trade";
        }

        /// <summary>
        /// Return the optimal contracts allowed for a given risk
        /// </summary>
        /// <param name="entryPrice"></param>
        /// <param name="stopLossPrice"></param>
        /// <returns></returns>
        public int Contracts(double entryPrice, double stopLossPrice)
        {
            // Check if entry price or stop loss price is 0
            if (entryPrice == 0 || stopLossPrice == 0) return 0;

            // return tradesize
            int tradeSize;
            double rawTradeSize = 0;

            // calculate price risk
            double priceRisk = Math.Abs(entryPrice - stopLossPrice);
            priceRisk = Math.Round(priceRisk, 5);

            // get raw trade size
            if (Category == ESymbolCategory.Cash || Category == ESymbolCategory.Forex)
            {
                // Get pip values.
                GetPipValues();

                // get fx calculation values
                double onePip = GetTickValue(10);
                double pipValue = PipValues[BaseSymbol];

                // calculate numerator and denominator values
                double valueRisk = (priceRisk / onePip) * pipValue;

                // obtain raw trade size
                rawTradeSize = (DollarRisk / valueRisk) * 100000;
               
            }
            else if (Category == ESymbolCategory.Stock)
            {
                rawTradeSize = DollarRisk / priceRisk;
            }
            else if (Category == ESymbolCategory.Future)
            {
                // incomplete
                double oneTick = GetTickValue(1);
                double valueRisk = priceRisk / oneTick * 12.5;

                rawTradeSize = DollarRisk / valueRisk;
            }

            // convert to min step
            rawTradeSize = StepSize * Math.Floor(rawTradeSize / StepSize);

            // check if min < contracts < max 
            if (rawTradeSize < MinimumContracts) rawTradeSize = 0;
            if (rawTradeSize > MaximumContracts) rawTradeSize = MaximumContracts;

            // round down 
            tradeSize = (int)rawTradeSize;
            return tradeSize;
        }

        #region Forex
        private void GetPipValues()
        {
            // clear all the data at the end of the session.
            // calculate end of session time.
            int sessionEndTime = 100 * (Bars.Sessions[0].EndTime.Hours) + Bars.Sessions[0].EndTime.Minutes;
            int currentSessionTime = 100 * (Bars.Time[0].Hour) + Bars.Time[0].Minute;

            // if current session time = end time, clear data.
            if (currentSessionTime == sessionEndTime) PipValues.Clear();

            string chartSymbol = Bars.Info.RTSymbolName;
            ESymbolCategory category = Bars.Info.Category;

            // current chart base symbols
            string[] _baseSymbol = chartSymbol.Split('.');
            string baseSymbol = _baseSymbol[1];

            // cache base symbol
            BaseSymbol = baseSymbol;

            // list of major forex base symbols
            string[] baseSymbols = { "USD", "JPY", "CHF", "CAD", "AUD", "GBP", "NZD" };

            // check if current symbol is within list of major forex base symbols
            if (baseSymbols.Contains(baseSymbol) == false) return;

            // If we have not calculated the pip value for this base symbol then.
            if (PipValues.ContainsKey(baseSymbol) == false)
            {
                double conversionFactor;
                switch (baseSymbol)
                {
                    default:
                        {
                            // return 0
                            conversionFactor = 0.0;
                            break;
                        }

                    case "USD":
                        {
                            conversionFactor = 1.0;
                            break;
                        }

                    case "JPY":
                        {
                            conversionFactor = 1.0 / GetSymbolPriceFromStorage("USD.JPY", category);
                            break;
                        }

                    case "CHF":
                        {
                            conversionFactor = 1.0 / GetSymbolPriceFromStorage("USD.CHF", category);
                            break;
                        }

                    case "CAD":
                        {
                            conversionFactor = 1.0 / GetSymbolPriceFromStorage("USD.CAD", category);
                            break;
                        }

                    case "GBP":
                        {
                            conversionFactor = GetSymbolPriceFromStorage("GBP.USD", category);
                            break;
                        }


                    case "AUD":
                        {
                            conversionFactor = GetSymbolPriceFromStorage("AUD.USD", category);
                            break;
                        }

                    case "NZD":
                        {
                            conversionFactor = GetSymbolPriceFromStorage("NZD.USD", category);
                            break;
                        }
                }

                // calculate the pip value for the current chart base symbol
                double pipValue = GetTickValue(10) * conversionFactor * 100000;

                // cache the results into Dictionary.
                PipValues.Add(baseSymbol, pipValue);
            }
        }

        double GetTickValue(int ticks)
        {
            // 10 ticks make one pip.
            double tickValue = Bars.Info.MinMove / Bars.Info.PriceScale;
            return tickValue * ticks;
        }

        private double GetSymbolPriceFromStorage(string symbolName, ESymbolCategory category)
        {
            ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

            double symbolData = 0;

            var benchmarkInfo = SymbolStorage
                .GetDataFeeds()
                .SelectMany
                (
                    data => SymbolStorage.GetSymbols(data, category)
                                         .Select(data2 => new { dataFeed = data, symInfo = data2 })
                )
                .Where(data => data.symInfo.SymbolName == symbolName)
                .First();

            InstrumentDataRequest request = Bars.Request;
            request.Range.From = Bars.Time[2];
            request.Range.To = Bars.Time[0];
            request.Category = (ESymbolCategory)benchmarkInfo.symInfo.SymbolCategory;
            request.Symbol = benchmarkInfo.symInfo.SymbolName;
            request.DataFeed = benchmarkInfo.dataFeed;
            request.Exchange = benchmarkInfo.symInfo.SymbolExchange;
            request.RTSymbol = request.Symbol;
            request.RTDataFeed = request.DataFeed;
            request.RTExchange = request.Exchange;

            // without autoEvent, the method will return before results are returned
            AutoResetEvent autoEvent = new AutoResetEvent(false);

            // this is foreground or worker thread.
            IDataLoaderResult results = DataLoader.BeginLoadData(request, result =>
            {
                // check to see if load data is completed
                if (result.IsCompleted)
                {
                    try
                    {
                        // lock to prevent other application from accessing concurrently
                        rwLock.EnterWriteLock();
                        foreach (var quote in result.Data)
                            symbolData = quote.Close;
                    }
                    finally
                    {
                        // remember to unlock
                        rwLock.ExitWriteLock();
                    }
                }

                DataLoader.EndLoadData(result);

                // return control to main thread
                autoEvent.Set();
            }
            , null);

            // wait for signal from worker thread to continue
            autoEvent.WaitOne();
            return symbolData;
        }
        #endregion

        #region Debug
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion
    }
}

namespace PowerLanguage.Indicator
{
    public class _SherNing_Functions : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : Function Library
        // Platform     : Multicharts
        // Class Type   : Library
        // Date         : 28 Nov 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Framework for building and testing functions
        // Remarks      : Use this framework to build and test functions before exporting
        //=========================================================================================


        public _SherNing_Functions(object _ctx) : base(_ctx) { }

        IPlotObjectStr OutputStatus;
        List<IPlotObject> Plot;
        IPlotObject PlotSingle;
        AutoPositionSizing AutoPos;

        protected override void Create()
        {
            AutoPos = new AutoPositionSizing(this);

            // plot objects
            OutputStatus = AddPlot(new StringPlotAttributes(
                "Output Status: ", Color.RoyalBlue, Color.Black));

            //Plot = new List<IPlotObject>();

            string[] plotNames = new string[]
            {
                "Plot 0", "Plot 1", "Plot 2", "Plot 3", "Plot 4",
                "Plot 5", "Plot 6", "Plot 7", "Plot 8",
                "Plot 9", "Plot 10"
            };


            //for (int i = 0; i <= 10; i++)
            //    Plot.Add(AddPlot(new PlotAttributes(
            //        plotNames[i], EPlotShapes.Line, Color.White, Color.Black, 3, EPlotStyle.Solid, true)));


            PlotSingle = AddPlot(new PlotAttributes("Single", EPlotShapes.Histogram, Color.RoyalBlue));
            Output.Clear();
        }

        protected override void StartCalc()
        {
            AutoPos.AccountSize = 100000;
            AutoPos.Risk = 2;
        }

        protected override void CalcBar()
        {
            if (Bars.LastBarOnChart)
            {
                AutoPos.Call();
                Print("Contracts: " + AutoPos.Contracts(3700,3690));
            }
        }

        #region Print Functions
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion
    }
}

#region Test Functions
namespace PowerLanguage.Function
{
    #region Understanding multicharts
    public sealed class TestSeries : FunctionSeries<int>
    {
        public TestSeries(CStudyControl m) : base(m)
        {

        }
        int Count;
        protected override void StartCalc()
        {
            Count = 0;
        }
        protected override int CalcBar()
        {
            // every single call() or .value, will invoke CalcBar once
            return Count++;
        }

        #region Print Functions
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion
    }

    public sealed class TestSimple : FunctionSimple<int>
    {
        public TestSimple(CStudyControl m) : base(m)
        {

        }
        int Count;
        protected override void StartCalc()
        {
            // TestSimple initialize once, state will not reset when start calc is called.
            Count = 0;
        }
        protected override int CalcBar()
        {
            // calcbar will be called explicitly
            return Count++;
        }

        #region Print Functions
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion

    }
    #endregion

}

#endregion


