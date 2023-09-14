﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.Reporting.WinForms;
using Microsoft.ReportingServices.Interfaces;
using WarehouseManagement.Models;

namespace WarehouseManagement.Views.Main.SystemSettingModule
{
    /// <summary>
    /// Interaction logic for WaybillJournal.xaml
    /// </summary>
    public partial class WaybillJournal : UserControl
    {
        public WaybillJournal()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ReportViewer1.LocalReport.ReportEmbeddedResource = "WarehouseManagement.Waybill.WaybillTemplate.rdlc";
                ReportViewer1.RefreshReport();
            }
            catch(Exception ex)
            {
                MessageBox.Show("Report rendering error: " + ex.Message);
            }
            
        }
    }
}
