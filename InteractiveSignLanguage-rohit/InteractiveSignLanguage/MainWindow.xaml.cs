using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Linq;
using System.Speech.Synthesis;
//using System.Speech.Recognition;
using System.Threading;
using Microsoft.Kinect;
using System.Windows.Controls;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using System.IO;
using Firebase.Database;
using Firebase.Database.Query;
using FireBaseLib;
namespace InteractiveSignLanguage
{
    public partial class MainWindow : Window
    {
        SpeechSynthesizer sSynth = new SpeechSynthesizer();
        //PromptBuilder pBuilder = new PromptBuilder();
       
       Microsoft.Speech.Recognition.SpeechRecognitionEngine sRecognize = new Microsoft.Speech.Recognition.SpeechRecognitionEngine();
       
        private const int RedIdx = 2;

        private const int GreenIdx = 1;

        private const int BlueIdx = 0;

        private const int Ignore = 2;

        private const int BufferSize = 32;
        
        private const int MinimumFrames = 6;

        private const int CaptureCountdownSeconds = 3;
        int flg = 0;
        int modeflg = 0;
    private readonly Dictionary<JointType, Brush> _jointColors = new Dictionary<JointType, Brush>
        { 
            {JointType.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointType.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointType.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {JointType.Head, new SolidColorBrush(Color.FromRgb(200, 0, 0))},
            {JointType.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79, 84, 33))},
            {JointType.ElbowLeft, new SolidColorBrush(Color.FromRgb(84, 33, 42))},
            {JointType.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointType.HandLeft, new SolidColorBrush(Color.FromRgb(215, 86, 0))},
            {JointType.ShoulderRight, new SolidColorBrush(Color.FromRgb(33, 79,  84))},
            {JointType.ElbowRight, new SolidColorBrush(Color.FromRgb(33, 33, 84))},
            {JointType.WristRight, new SolidColorBrush(Color.FromRgb(77, 109, 243))},
            {JointType.HandRight, new SolidColorBrush(Color.FromRgb(37,  69, 243))},
            {JointType.HipLeft, new SolidColorBrush(Color.FromRgb(77, 109, 243))},
            {JointType.KneeLeft, new SolidColorBrush(Color.FromRgb(69, 33, 84))},
            {JointType.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {JointType.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointType.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {JointType.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222, 76))},
            {JointType.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {JointType.FootRight, new SolidColorBrush(Color.FromRgb(77, 109, 243))}
        };

       
        
        
        private bool _capturing;

        /// <summary>
        /// Dynamic Time Warping object
        /// </summary>
        private DtwGestureRecognizer _dtw;

        /// <summary>
        /// How many frames occurred 'last time'. Used for calculating frames per second
        /// </summary>
        private int _lastFrames;

        /// <summary>
        /// The 'last time' DateTime. Used for calculating frames per second
        /// </summary>
        public static DateTime _lastTime = DateTime.MaxValue;

        /// <summary>
        /// The Natural User Interface runtime
        /// </summary>
        private KinectSensor _nui;

        /// <summary>
        /// Total number of framed that have occurred. Used for calculating frames per second
        /// </summary>
        private int _totalFrames;

        /// <summary>
        /// Switch used to ignore certain skeleton frames
        /// </summary>
        private int _flipFlop;

        /// <summary>
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        /// </summary>
        private ArrayList _video;
        int m;
        /// <summary>
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        /// </summary>
        private DateTime _captureCountdown = DateTime.Now;

        /// <summary>
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        /// </summary>
        private System.Windows.Forms.Timer _captureCountdownTimer;
        private EventHandler<SpeechRecognizedEventArgs> sRecognize_SpeechRecognize;

        /// <summary>
        /// Initializes a new instance of the MainWindow class
        /// </summary>
        
        public MainWindow()
        {
            InitializeComponent();
            m = _captureCountdown.Month;
        }

        public void LoadGesturesFromFile(string fileLocation)
        {
            int itemCount = 0;
            string line;
            string gestureName = String.Empty;

            // TODO I'm defaulting this to 12 here for now as it meets my current need but I need to cater for variable lengths in the future
            ArrayList frames = new ArrayList();
            double[] items = new double[12];
          
            // Read the file and display it line by line.
            System.IO.StreamReader file = new System.IO.StreamReader(fileLocation);
            while ((line = file.ReadLine()) != null)
            {
                if (line.StartsWith("@"))
                {
                    gestureName = line;
                    continue;
                }
                if (line == "")
                {
                    continue;

                }
                if (line.StartsWith("~"))
                {
                    frames.Add(items);
                    itemCount = 0;
                    items = new double[12];
                    continue;
                }
            
                if (!line.StartsWith("----"))
                {
                    items[itemCount] = Double.Parse(line);
                }
               
                itemCount++;

                if (line.StartsWith("----"))
                {
                    _dtw.AddOrUpdate(frames, gestureName);
                    frames = new ArrayList();
                    gestureName = String.Empty;
                    itemCount = 0;
                }
               
            }

            file.Close();
        }

        /// <summary>
        /// Called each time a skeleton frame is ready. Passes skeletal data to the DTW processor
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Skeleton Frame Ready Event Args</param>
        private static void SkeletonExtractSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (var skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null) return; // sometimes frame image comes null, so skip it.
                var skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                skeletonFrame.CopySkeletonDataTo(skeletons);
               
                foreach (Skeleton data in skeletons)
                {
                    Skeleton2DDataExtract.ProcessData(data);
                
                }
            }
        }

        /// <summary>
        /// Gets the display position (i.e. where in the display image) of a Joint
        /// </summary>
        /// <param name="joint">Kinect NUI Joint</param>
        /// <returns>Point mapped location of sent joint</returns>
        private Point GetDisplayPosition(Joint joint)
        {
            float depthX, depthY;
            //var pos = _nui.MapSkeletonPointToDepth(joint.Position, DepthImageFormat.Resolution320x240Fps30);
            var pos = _nui.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, DepthImageFormat.Resolution320x240Fps30);

            depthX = pos.X;
            depthY = pos.Y;

            int colorX, colorY;

            // Only ImageResolution.Resolution640x480 is supported at this point
            var pos2 = _nui.CoordinateMapper.MapSkeletonPointToColorPoint(joint.Position, ColorImageFormat.RgbResolution640x480Fps30);
            colorX = pos2.X;
            colorY = pos2.Y;

            // Map back to skeleton.Width & skeleton.Height
            return new Point((int)(skeletonCanvas.Width * colorX / 640.0), (int)(skeletonCanvas.Height * colorY / 480));
        }

        /// <summary>
        /// Works out how to draw a line ('bone') for sent Joints
        /// </summary>
        /// <param name="joints">Kinect NUI Joints</param>
        /// <param name="brush">The brush we'll use to colour the joints</param>
        /// <param name="ids">The JointsIDs we're interested in</param>
        /// <returns>A line or lines</returns>
        private Polyline GetBodySegment(JointCollection joints, Brush brush, params JointType[] ids)
        {

            var points = new PointCollection(ids.Length);
            foreach (JointType t in ids)
            {
                points.Add(GetDisplayPosition(joints[t]));
            }

            var polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
        }

        /// <summary>
        /// Runds every time a skeleton frame is ready. Updates the skeleton canvas with new joint and polyline locations.
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Skeleton Frame Event Args</param>
        private void NuiSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons;
            using (var frame = e.OpenSkeletonFrame())
            {
                if (frame == null) return;
                skeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(skeletons);
            }
          
            int iSkeleton = 0;
            var brushes = new Brush[6];
            brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
            brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
            brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
            brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

            skeletonCanvas.Children.Clear();
           
            foreach (var data in skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    flg = 1;
                    // Draw bones
                    Brush brush = brushes[iSkeleton % brushes.Length];
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointType.HipCenter, JointType.Spine, JointType.ShoulderCenter, JointType.Head));
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointType.ShoulderCenter, JointType.ShoulderLeft, JointType.ElbowLeft, JointType.WristLeft, JointType.HandLeft));
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointType.ShoulderCenter, JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight));
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointType.HipCenter, JointType.HipLeft, JointType.KneeLeft, JointType.AnkleLeft, JointType.FootLeft));
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointType.HipCenter, JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight));

                    // Draw joints
                    foreach (Joint joint in data.Joints)
                    {
                        Point jointPos = GetDisplayPosition(joint);
                        var jointLine = new Line();
                        jointLine.X1 = jointPos.X - 3;
                        jointLine.X2 = jointLine.X1 + 6;
                        jointLine.Y1 = jointLine.Y2 = jointPos.Y;
                        jointLine.Stroke = _jointColors[joint.JointType];
                        jointLine.StrokeThickness = 6;
                        skeletonCanvas.Children.Add(jointLine);

                    }

                    float hrz = data.Joints[JointType.HandRight].Position.Z;
                    float sp = data.Joints[JointType.Spine].Position.Z;
                    float diff = sp - hrz;

                    if (diff > 0.5)
                    {
                        sSynth.Speak("stop");
                    }
                    //  txtval.Text = diff + "";
                }
                
                iSkeleton++;
            } // for each skeleton
            if (flg == 0)
            {
               // pBuilder.ClearContent();
                //pBuilder.AppendText("Please stand infront of kinect camera.");

                sSynth.Speak("Please stand infront of kinect camera.");
                //sSynth.Speak(pBuilder);
                flg = 1;
            }  
        }
        public void initspeech()
        {

            Choices command = new Choices();
       //     command.Add("welcome");
        //    command.Add("start");
            command.Add("Stop");
            command.Add("Bye Bye");
            command.Add("Please");
            command.Add("switch mode");
      //      command.Add("she is crying");
            command.Add("walking");
            //      command.Add("watching you"); 
            // ***new commands***

            command.Add("Hello");
            command.Add("How are you");
            command.Add("Thank you");
            command.Add("Nice to meet you");
            command.Add("Sorry");
            command.Add("Happy");
            command.Add("Wonderful");
            command.Add("No");
            command.Add("Ok");
            command.Add("Understand");
            command.Add("K V N Naik");

            var gb = new GrammarBuilder { Culture = sRecognize.RecognizerInfo.Culture };
            gb.Append(command);
            Grammar gr = new Grammar(gb);
            
            try
            {
                 RecognizerInfo ri = GetKinectRecognizer();

          
               // recognitionSpans = new List<Span> { forwardSpan, backSpan, rightSpan, leftSpan };

                sRecognize = new SpeechRecognitionEngine(ri.Id);

                sRecognize.RequestRecognizerUpdate();
                sRecognize.LoadGrammar(gr);
                sRecognize.SpeechRecognized += sRecognize_SpeechRecognized;
                sRecognize.SetInputToDefaultAudioDevice();
                sRecognize.SetInputToAudioStream(_nui.AudioSource.Start(), new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                sRecognize.RecognizeAsync(RecognizeMode.Multiple);
            }

            catch
            {
                return;
            }
            
        }
        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }
        private void sRecognize_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence >= 0.3)
            {

                //var ss = e.Result.Semantics;
                label2.Content = e.Result.Text.ToString();

                switch (e.Result.Text.ToLower())
                {
                    case "switch mode":
                        if (modeflg == 0)
                        {
                            modeflg = 1;
                            label3.Content = "ordinary mode active";
                        }
                        else
                        {
                            modeflg = 0;
                            label3.Content = "impaired mode active";
                        }
                         break;
                    case "please":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("please.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show("please");
                        break;
                    case "start":
                       // MessageBox.Show(e.Result.Text);
                        break;
                    case "stop":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("stop.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                       
                        break;
                    case "bye bye":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("bye.gif",UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                       // MessageBox.Show(e.Result.Text);

                        break;
  
                    case "hello":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("hello.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;
                    case "walking":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("walk.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;
                    case "how are you":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("how-are-you.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "thank you":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("thank-you.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "nice to meet you":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("nice-to-meet-you.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "sorry":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("sorry.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "happy":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("happy.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "wonderful":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("wonderful.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "no":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("no.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "ok":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("ok.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "understand":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("understand.gif", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;

                    case "k v n naik":
                        if (modeflg == 1)
                        {
                            Uri u = new Uri("kvn-naik.png", UriKind.Relative);
                            mediaElement1.Source = u;
                            mediaElement1.Play();
                        }
                        // MessageBox.Show(e.Result.Text);
                        break;
                }
            }
            else
            {
                label2.Content = "--";
            }
        }
       
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _nui = (from i in KinectSensor.KinectSensors
                        where i.Status == KinectStatus.Connected
                        select i).FirstOrDefault();

                var fb1 = FireBaseDB.init(Config.url);
                var fb = FireBaseDB.init(Config.Baseurl);
                var task = fb.Child("rohitsign1").OnceAsync<Item>();
                task.Wait();
                var fbdata = task.Result;

                foreach (var data in fbdata)
                {

                    Config.basePath = data.Object.BasePath;
                }
              
                if (_nui == null)
                {
                    MessageBox.Show("No kinectes connected!");
                    throw new NotSupportedException("No kinectes connected!");
                }


                try
                {
                    _nui.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                    _nui.SkeletonStream.Enable();
                    _nui.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    _nui.Start();
                }
                catch (InvalidOperationException)
                {
                    System.Windows.MessageBox.Show("Runtime initialization failed. Please make sure Kinect device is plugged in.");
                    return;
                }

                _lastTime = DateTime.Now;

                _dtw = new DtwGestureRecognizer(12, 0.9, 2, 2, 10);
                _video = new ArrayList();

                // If you want to see the depth image and frames per second then include this
                // I'mma turn this off 'cos my 'puter is proper slow
                //_nui.DepthFrameReady += NuiDepthFrameReady;

                _nui.SkeletonFrameReady += NuiSkeletonFrameReady;
                _nui.SkeletonFrameReady += SkeletonExtractSkeletonFrameReady;

                // If you want to see the RGB stream then include this
               // _nui.ColorFrameReady += NuiColorFrameReady;
                Skeleton2DDataExtract.Skeleton2DdataCoordReady += NuiSkeleton2DdataCoordReady;

               // Skeleton2DDataExtract.Skeleton2DdataCoordReady+=new Skeleton2DDataExtract.Skeleton2DdataCoordEventHandler(Skeleton2DDataExtract_Skeleton2DdataCoordReady);
                // Update the debug window with Sequences information

               // txtout.Text = _dtw.RetrieveText();
                initspeech();
                Debug.WriteLine("Finished Window Loading");
            }
            catch (Exception ex)
            {
                throw new NotSupportedException("kinectes power issue!");
            }
        }
 

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("Stopping NUI");
                _nui.Stop();
                Debug.WriteLine("NUI stopped");
                Environment.Exit(0);
            }
            catch { }

        }

      
        private void NuiSkeleton2DdataCoordReady(object sender, Skeleton2DdataCoordEventArgs a)
        {
           // currentBufferFrame.Text = _video.Count.ToString();

            // We need a sensible number of frames before we start attempting to match gestures against remembered sequences
            if (_video.Count > MinimumFrames && _capturing == false)
            {
                ////Debug.WriteLine("Reading and video.Count=" + video.Count);
                string s = _dtw.Recognize(_video);
              //  results.Text = "Recognised as: " + s;
                if (!s.Contains("__UNKNOWN"))
                {
                    // There was no match so reset the buffer
                    _video = new ArrayList();
                    label2.Content =  " " + s.Substring(1);

                 //   pBuilder.ClearContent();
                 //   pBuilder.AppendText(s.Substring(1));
                    sSynth.Speak(s.Substring(1));

                }
            }

            // Ensures that we remember only the last x frames
            if (_video.Count > BufferSize)
            {
                // If we are currently capturing and we reach the maximum buffer size then automatically store
                if (_capturing)
                {
                    DtwStoreClick_Click(null, null);
                }
                else
                {
                    // Remove the first frame in the buffer
                    _video.RemoveAt(0);
                }
            }

            // Decide which skeleton frames to capture. Only do so if the frames actually returned a number. 
            // For some reason my Kinect/PC setup didn't always return a double in range (i.e. infinity) even when standing completely within the frame.
            // TODO Weird. Need to investigate this
            if (!double.IsNaN(a.GetPoint(0).X))
            {
                // Optionally register only 1 frame out of every n
                _flipFlop = (_flipFlop + 1) % Ignore;
                if (_flipFlop == 0)
                {
                    _video.Add(a.GetCoords());
                }
            }

            // Update the debug window with Sequences information
            //dtwTextOutput.Text = _dtw.RetrieveText();
        }

        private void CaptureCountdown(object sender, EventArgs e)
        {
            if (sender == _captureCountdownTimer)
            {
                if (DateTime.Now < _captureCountdown)
                {
                    status.Content = "Wait " + ((_captureCountdown - DateTime.Now).Seconds + 1) + " seconds";
                }
                else
                {
                    _captureCountdownTimer.Stop();
                    status.Content = "Recording gesture";
                    StartCapture();
                }
            }
        }

        /// <summary>
        /// Capture mode. Sets our control variables and button enabled states
        /// </summary>
        private void StartCapture()
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            btnrecord.IsEnabled = false;
            DtwStoreClick.IsEnabled = true;

            // Set the capturing? flag
            _capturing = true;

            ////_captureCountdownTimer.Dispose();

            status.Content = "Recording gesture";

            // Clear the _video buffer and start from the beginning
            _video = new ArrayList();
        }

        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
       

        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
      

        /// <summary>
        /// Loads the user's selected gesture file
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
       
        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
      
        private void btnrecord_Click(object sender, RoutedEventArgs e)
        {
            if (txtaction.Text == "")
            {
               // MessageBox.Show("Please Enter action name");
                return;
            }
            _captureCountdown = DateTime.Now.AddSeconds(CaptureCountdownSeconds);

            _captureCountdownTimer = new System.Windows.Forms.Timer();
            _captureCountdownTimer.Interval = 50;
            _captureCountdownTimer.Start();
            _captureCountdownTimer.Tick += CaptureCountdown;
        }
  
        private void DtwStoreClick_Click(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            btnrecord.IsEnabled = true;
            DtwStoreClick.IsEnabled = false;

            // Set the capturing? flag
            _capturing = false;

            //status.Text = "Remembering " + gestureList.Text;
            //status.Text = "Remembering " + textBox3.Text;

            // Add the current video buffer to the dtw sequences list
               _dtw.AddOrUpdate(_video, "@"+txtaction.Text);
           // _dtw.AddOrUpdate(_video, textBox3.Text);

           // results.Text = "Gesture " + textBox3.Text + "added";

            // Scratch the _video buffer
            _video = new ArrayList();

            // Switch back to Read mode
            dtwRead_Click(null, null);
        
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
             string s = _dtw.RetrieveText();
             try
             {
                 StreamWriter sw = new StreamWriter("d:\\mygesture.txt", true);
                 string[] content = s.Split('\n');
                     for(int i=0;i<content.Length;i++)
                     {
                         sw.WriteLine(content[i]);
                     }
                // System.IO.File.WriteAllText("e:\\mygesture.txt", s);
                 sw.Close();
                 status.Content = "Saved in file ";
             }
             catch
             {
             }
        }

        private void dtwLoadFile_Click(object sender, RoutedEventArgs e)
        {

            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extensionE:\proj work\kinect codes\KinectSL ver1.2 - A\KinectSL ver1.2 - A\DTWGestureRecognition\ImageFrameCommonExtensions.cs
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text documents (.txt)|*.txt";

          //  dlg.InitialDirectory = GestureSaveFileLocation;

            // Display OpenFileDialog by calling ShowDialog method
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == true)
            {
                // Open document
                LoadGesturesFromFile(dlg.FileName);
                //txtout.Text = _dtw.RetrieveText();
                status.Content = "Gestures loaded!";
            } 
        
        }

        private void dtwRead_Click(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            btnrecord.IsEnabled = true;
            DtwStoreClick.IsEnabled = false;

            // Set the capturing? flag
            _capturing = false;

            // Update the status display
            status.Content = "Reading";
        }

        private void dtwRead_Click_1(object sender, RoutedEventArgs e)
        {

        }


    }
}





