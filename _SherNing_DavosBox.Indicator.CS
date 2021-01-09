using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using PowerLanguage.Function;

#region MultiChart Function Library

namespace PowerLanguage.Function
{
   
}
#endregion

#region Test Framework

namespace PowerLanguage.Indicator
{
    //==========================================================================================
    // Name           : Strategy Tester
    // Description    : Use Text objects to backtest any strategy
    // Version        : v.1.0
    // Date Created   : 10 - Jun - 2020
    // Time Taken     : 
    // Remarks        :
    //==========================================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==========================================================================================

    /*  ----------------------------------- Version History -----------------------------------
     *   v.1.0
     *   - Learn and develop my own approach to solving multichart strategies confidently
     *   
     *  ----------------------------------- Version History -----------------------------------
     

     *  ---------------------------------------- Notes ----------------------------------------
     *  -
     *  
     *   
     *  ---------------------------------------- Notes ----------------------------------------
     */
    public class _SherNing_DavosBox : IndicatorObject
    {
        // class properties
        [Input] public int Length { get; set; }

        public _SherNing_DavosBox (object _ctx) : base(_ctx)
        {
            Length = 6;
        }

        // class fields
        protected override void Create()
        {
            Output.Clear();
        }
        protected override void StartCalc()
        {
        }
        protected override void CalcBar()
        {
        }

        protected override void StopCalc()
        {
            Output.Clear();
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

        #region ToolStrip Framework

        #region ToolStrip Initialization
        // ---------------------------- StartCalc Start ---------------------------- //

        private bool IsToolStripCreated;
        private void ToolStripStartCalc()
        {
            // place this method in startcalc.
            if (IsToolStripCreated == false)
            {
                ChartToolBar.AccessToolBar(ToolStripMain);
                IsToolStripCreated = true;
            }

            // reset toolstrip variables here
            IsDisplayText = false;
        }
        // ---------------------------- StartCalc End ---------------------------- //
        #endregion

        #region ToolStrip Main Function
        // ---------------------------- Main Start ---------------------------- //
        private ToolStripButton DisplayTexts;
        private void ToolStripCreate(ToolStrip tb)
        {
            // Create Toolstrip objects here and in order of display.

            ToolStripLabel label1 = new ToolStripLabel();
            label1.Text = "Display Text";
            AddNewItemToToolStrip(tb, label1);

            DisplayTexts = new ToolStripButton();
            AddNewItemToToolStrip(tb, DisplayTexts);
        }

        private bool IsDisplayText;
        private void ToolStripMain(ToolStrip tb)
        {
            // main toolstrip adjustments first
            tb.AutoSize = false;
            tb.Height = 100;
            tb.TabIndex = 0;

            // initialize tool strip objects
            ToolStripCreate(tb);

            // Write functionality here.
            DisplayTexts.Text = "Show";
            DisplayTexts.BackColor = Color.LimeGreen;
            DisplayTexts.ForeColor = Color.White;
            DisplayTexts.Click += delegate
            {
                if (IsDisplayText == true)
                {
                    DisplayTexts.Text = "Show";
                    DisplayTexts.BackColor = Color.LimeGreen;
                    DisplayTexts.ForeColor = Color.White;
                    IsDisplayText = false;
                }

                // if false, hide text
                else
                {
                    DisplayTexts.Text = "Hide";
                    DisplayTexts.BackColor = Color.Red;
                    DisplayTexts.ForeColor = Color.White;
                    IsDisplayText = true;
                }
            };
        }
        // ---------------------------- Main End ---------------------------- //
        #endregion

        #region ToolStrip Dispose
        // ---------------------------- No changes required to be made here ---------------------------- //
        private void AddNewItemToToolStrip(ToolStrip tb, ToolStripItem item)
        {
            item.Tag = this;
            tb.Items.Add(item);
        }
        protected override void Destroy()
        {
            // place this method in Destroy()
            if (IsToolStripCreated)
                ChartToolBar.AccessToolBar(RemoveToolBar);
        }
        private void RemoveToolBar(ToolStrip tb)
        {
            List<ToolStripItem> items = new List<ToolStripItem>();
            foreach (ToolStripItem item in tb.Items)
            {
                if (ReferenceEquals(this, item.Tag)) items.Add(item);
            }

            // Clear all tool bar items
            foreach (var item in items) tb.Items.Remove(item);
        }
        #endregion

        #endregion
    }
}

#endregion

#region Function Notes

/* / -------------------------- Function Notes  -------------------------- /
 * 
 *                  VariableSeries<T> as a Property
 *  You can get and set variable series as a Function class Property
 *  Which you can get from the caller. However, you need to use .Call()
 *  To invoke the Function class to calculate before getting the return
 *  value. Everytime, when you need to reference previous bar's calculations,
 *  it is best practice to use VariableSeries<T>
 *  
 *                  List<T> Insert(index, data) Method
 *  Inserting is adding. Adding at a certain index, rather than replacing.
 *  
 *                  Function return value property 
 *  Getting Function Property Value akins to calling CalcBar() in the function.
 *  So if you call value 3 times, it will call CalcBar three times. 
 *  In the case of hull when you are doing inserting. It will insert the same price
 *  bar three times. To remedy this problem, in the calling method, 
 *  save the return value to a VariableSeries. 
 *  
 *                  Debug Print inside function works
 *  You can debug within the function itself. You can use the default print method.
 *  
 *                  Naming Conventions for Multichart Projects
 *   _SNi : Indicator     _SNs : Strategy         _SNf : Function
 *   Create a class for each. Indicator for plotting. Strategy for sending orders.
 *   Create a class framework for functions. Use region and versions to keep track
 *   of different version. V10, the 1 is changed if the outcome is different. 0, refactor.
 *   For Client projects, put the functions in the same class as the indicator or strategy.
 *   
 *                  File/Project Management
 *   Keep all classes and resources required in one file. A single file is a project.
 *   Store all powerlanguage and custom functions under the same file.
 *   Even if i reuse the file, rename it to follow file name. One file - One project.
 *   For my own files. Keep all the functions here. Every client will have their own Function Lib.
 *   Function lib is where the key calculation logic are stored. Indicator functions and strategy
 *   functions should be separated. Practice and improve my workflow for keeping version history.
 *   Latest version will not have vX.X it will be the original name.
 *   First digit, for new features. Second digit, for refactorization or debugging.
 *   
 *                  Understanding Function Call()
 *   The first time a function is called is Bars.Current > 1 or 2.
 *   The function Call() is implicitly called once before an indicator/strategy CalcBar() is called.
 *   Note: Current Bar is the first bar that Calcbar is called. Bars before current bar are MaxBarBack Bars.
 *   These bars are needed to needed to make the first calculation of Calcbar().
 *   Every single time Function.Value is called. a return CalcBar() is called on the function. BECAREFUL.
 *   If Value is called, then implicit Call() will NOT be called.
 *   Note: double Call(int barsago) == x[barsago].Value They are the same thing.
 *   
 *                  Debuging Functions.
 *   You don't have to remove and re-insert an indicator, to see changes made to the function
 *   if you are just printing the results to Output window. This is the best way to debug.
 *   
 *                  Open - Close Principle for design patterns.
 *   Open for modification, but closed for making changes within the class.
 *   When designing classes. I have to bear in mind the open close principle.
 *   
 *                  ISeries<double> 
 *   is an interface that any member who implements it will have an indexer and value property.
 *   the indexer[int barsago], gives you access to the value of which ever bars back using the 
 *   value property. Variable series implements this interface, it adds set to value.
 *   ISeries<double> is pegged to bar number, you can call it many times, you will get the same value.
 *   So long as you are calling it in the same BarNumber. Unlike Count, which increments every time,
 *   CalcBar() is called. classes that implements ISeries<double> works like a list. 
 *   It is passing the reference the reference of the list by property accessor.
 *   When dealing with price data, best is to use variable series, especially if you are working with
 *   non bar close data.
 *   
 *   Difference between FunctionSimple vs FunctionSeries
 *   There is one noticable difference. Is that FunctionSeries will implicityly Call().
 *   While FunctionSimple will not explicitly Call(). This becomes apparent when you are not using 
 *   the return value from CalcBar, rather when you are getting values from the property.
 *   Another key to FunctionSeries is that it remembers its state. So, you can use This[1] to retrive,
 *   reprevious CalcBar Value.
 *   
 *   Additional Notes on VariableSeries<double>
 *   VariableSeries<> is linked and synced with the current bar.
 *   If values will persist from bar to bar, unless it is updated. In other words, at the end of each
 *   closing tick, Variable Series will take the prev calculated value, or the updated new value.
 *   It is like a List which add or inserts once every end of bar implicityly.
 *
 */
#endregion

