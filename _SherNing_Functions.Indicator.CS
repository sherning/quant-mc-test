using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    public sealed class DeveloperToolKit : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Developer Tool Kit
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
         *  28/11/2020 - ISeries
         * 
         *======================================== Version ========================================
        /*/
        public DeveloperToolKit(CStudyControl m) : base(m) { }

        public double HighestPrice(ISeries<double> price, int barsBack)
        {
            double high = 0;

            // exclude current bar high
            for (int i = 0; i < barsBack; i++)
                high = Math.Max(high, price[i]);

            return high;
        }

        protected override string CalcBar()
        {
            return "Does not return any value";
        }
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
        // Remarks      : Use this framework to build and test functions
        //=========================================================================================

        /*/
         *======================================== Version ========================================
         *  28/11/2020 - Statistics
         * 
         *======================================== Version ========================================
        /*/
        public _SherNing_Functions(object _ctx) : base(_ctx) { }

        IPlotObjectStr OutputStatus;
        List<IPlotObject> Plot;

        Statistics Stats;
        DeveloperToolKit DevTool;
        protected override void Create()
        {
            OutputStatus = AddPlot(new StringPlotAttributes(
                "Output Status: ", Color.RoyalBlue, Color.Black));

            Plot = new List<IPlotObject>();

            string[] plotNames = new string[]
            {
                "Plot 0", "Plot 1", "Plot 2", "Plot 3", "Plot 4",
                "Plot 5", "Plot 6", "Plot 7", "Plot 8",
                "Plot 9", "Plot 10"
            };


            for (int i = 0; i <= 10; i++)
            {
                Plot.Add(AddPlot(new PlotAttributes(
                    plotNames[i], EPlotShapes.Line, Color.White, Color.Black, 3, EPlotStyle.Solid, true)));
            }

            Stats = new Statistics(this);
            DevTool = new DeveloperToolKit(this);

            Output.Clear();
        }

        protected override void StartCalc()
        {

        }
        protected override void CalcBar()
        {
            if (Bars.CurrentBar == 2)
            {
                // currentbar == 1 called twice.
                Test2();
            }

            double highestPrice = DevTool.HighestPrice(Bars.High, 3);
            double highestHigh = Bars.High.Highest(3);

            Plot[1].Set(highestPrice);
            Plot[2].Set(highestHigh);
        }

        private void Test2()
        {
            double[] set1 = Stats.GetArray(43, 21, 25, 42, 57, 59);
            double[] set2 = Stats.GetArray(99, 65, 79, 75, 87, 81);
            Print("Covariance: " + Stats.Covariance(set1, set2));
            Print("Correlation: " + Stats.CorrelationCoefficient(set1, set2));
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