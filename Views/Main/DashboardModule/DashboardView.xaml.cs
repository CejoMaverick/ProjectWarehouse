﻿using LiveCharts.Defaults;
using LiveCharts.Wpf;
using LiveCharts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WarehouseManagement.Views.Main.DashboardModule
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : Page
    {
        public DashboardView()
        {
            InitializeComponent();
            setChart();
        }


        private void setChart()
        {
            ChartValues < ObservableValue > revenueData = new ChartValues<ObservableValue>();
            Random random = new Random();
            int daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
            for (int day = 1; day <= daysInMonth; day++)
            {
                double revenue = random.Next(1000, 5000); // Random revenue value between 1000 and 5000
                revenueData.Add(new ObservableValue(revenue));
            }

            // Set the sample data to the salesChart
            salesChart.Series.Clear();
            salesChart.Series.Add(new LineSeries
            {
                Title = "Total Revenue",
                Values = revenueData
            });
        }

        private void endDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
