﻿using System;
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

using HDFace3dTracking.ViewModels;

namespace HDFace3dTracking
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        
        private MainViewModel ViewModel;

        public MainWindow()
        {
            InitializeComponent();
           
            ViewModel = new MainViewModel(Back);
            Back.Height = viewport3d.Height;
            Back.Width = viewport3d.Width;
            this.DataContext = this.ViewModel;
            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.ViewModel.StartCommand();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.ViewModel.StopCommand();
        }
    }
}
