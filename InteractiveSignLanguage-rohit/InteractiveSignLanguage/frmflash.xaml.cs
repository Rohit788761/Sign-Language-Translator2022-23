using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Speech.Synthesis;
using System.Windows.Threading;

namespace InteractiveSignLanguage
{
    /// <summary>
    /// Interaction logic for frmflash.xaml
    /// </summary>
    public partial class frmflash : Window
    {
        SpeechSynthesizer sSynth = new SpeechSynthesizer();
      //  PromptBuilder pBuilder = new PromptBuilder();
        DispatcherTimer dispatcherTimer;
        int cnt = 0;
        public frmflash()
        {
            InitializeComponent();
             dispatcherTimer = new DispatcherTimer();

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);

            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

            dispatcherTimer.Start();
        
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            cnt++;
            if (cnt == 1)
            {
                sSynth.Speak("Welcome to interactive sign language system.");
                dispatcherTimer.Stop();
                this.Hide();
                MainWindow m = new MainWindow();
                m.Show();
             
            }
          

        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
         
        }

        private void image1_Loaded(object sender, RoutedEventArgs e)
        {
           
        }
    }
}
