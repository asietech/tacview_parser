using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
 
using System.Windows.Media;
using System.Windows.Shapes;

namespace IL2radar
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
  
    public partial class MainWindow : Window
    {

        

        //worker thread
        protected Thread Readfile_thread;

        FileStream fs = null;

        public MainWindow()
        {
            InitializeComponent();

            stopButton.IsEnabled = false;


            //start the UI Update thread
            Readfile_thread = new Thread(Readfile_main)
            {
                IsBackground = true // So that a failed connection attempt 
            };
            // wont prevent the process from terminating while it does the long timeout

            Readfile_thread.Start();

            EventManager.RegisterClassHandler(typeof(Window),
            System.Windows.Input.Keyboard.KeyUpEvent, new System.Windows.Input.KeyEventHandler(keyUp), true);

        }

        private bool reading = false; 

        public class Objet : IEquatable<Objet>
        {
            public string ObjetName { get; set; }

            public Int32 ObjetId { get; set; }

            public double distance { get; internal set; }//meter
            public string Coalition { get; internal set; }

            public float latitude, longitude, altitude;

            public float update_time;
            internal double bearing;
            internal float heading;
            internal int ObjetType;

            public override string ToString()
            {
                return "ID: " + ObjetId + "   Name: " + ObjetName;
            }
            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                Objet objAsPart = obj as Objet;
                if (objAsPart == null) return false;
                else return Equals(objAsPart);
            }
            public override int GetHashCode()
            {
                return  ObjetId;
            }
            public bool Equals(Objet other)
            {
                if (other == null) return false;
                return (this.ObjetId.Equals(other.ObjetName));
            }
            // Should also override == and != operators.
        }

        private int index_objet = 0;
        private string my_id;
        private string my_coalition;
        private float my_latitude;
        private float my_longitude;
        private float home_latitude;
        private float home_longitude;
        private float my_heading;
        private float my_altitude;
        private float my_current_time=0;
        private float next_check_time = 0;
        
       
        bool visible = true;
         
        private void keyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.LeftShift)
            {
                if (visible)
                {

                    this.Height = 0;

                    visible = false;
                }
                else
                {

                    this.Height = 108;
                    visible = true;
                }
            }
        }

        private double Bearing_fromA_toB(float latitudeA,float longitudeA, float latitudeB,float longitudeB)
        {
            double teta1 = DegreesToRadians(latitudeA);
            double teta2 = DegreesToRadians(latitudeB);
            double delta2 = DegreesToRadians( longitudeB - longitudeA);

            //==================Heading Formula Calculation================//

            double y = Math.Sin(delta2) * Math.Cos(teta2);
            double x = Math.Cos(teta1) * Math.Sin(teta2) - Math.Sin(teta1) * Math.Cos(teta2) * Math.Cos(delta2);
            double brng = Math.Atan2(y, x);
            brng = brng * 180.00 / Math.PI;// radians to degrees
            brng = (((int)brng + 360) % 360);
            return brng;
        }
 
        private void Readfile_main()
        {
            ConcurrentDictionary<int, int> parts;
           // try
            {
                
                while (true)
                {
                    parts = new ConcurrentDictionary<int, int>();

                    List<Objet> objets = new List<Objet>();

                    //List<Objet> updated_objets = new List<Objet>();

                    if (reading)
                    {
                        string line;

                        StreamReader Textfile = new StreamReader(fs, Encoding.Default);

                        index_objet = 0;
                        my_id=null;
                        my_coalition=null;
                        my_latitude=0;
                        my_longitude=0;
                        my_altitude=0;
                        my_current_time = 0;
                        

                        //ignore le debut du fichier

                        while ((line = Textfile.ReadLine()) != "#0.00") ;


                        bool warning_enemy=false;     

                        while (reading)
                        {
                            //Thread.Sleep(25);
                            
                            while ((line = Textfile.ReadLine()) != null)                             
                            {
                                string[] words = line.Split(',');

                                //check if new time frame   
                                if (words[0].StartsWith("#"))
                                {
                                    try
                                    {       
                                        //calcul des positions enemies
                                        double distance = 10000, bearing = 0, altitude = 0, heading = 0;
                                        byte counter = 0;
                                        string enemi_name="";

                                        foreach (var plane in objets)
                                        {
                                            if ((plane.Coalition!=my_coalition)
                                                &&(plane.update_time>(my_current_time-5)))
                                            {
                                                if (plane.distance < distance)
                                                {
                                                    counter++;
                                                    distance = Math.Round(plane.distance);
                                                    bearing = Math.Round(plane.bearing);
                                                    altitude = Math.Round(plane.altitude - my_altitude);
                                                    heading = Math.Round(plane.heading);
                                                    //enemy_detected = true;
                                                    enemi_name = plane.ObjetName;
                                                  
                                                }
                                            }
                                        }
                                        if (counter>0)
                                        {
                                            warning_enemy = true;
                                            this.Dispatcher.BeginInvoke((Action)(() =>
                                            {
                                                label_distance.Content = distance.ToString();
                                                label_altidude.Content = altitude.ToString();
                                                label_time.Content = my_current_time.ToString();
                                                label_count.Content = counter.ToString();
                                                LabelBearing.Content = bearing.ToString();
                                                double elevation = Math.Atan2(altitude, distance);
                                                elevation = 180 * elevation / Math.PI;

                                                if (heading < 0) heading += 360;
                                                label_heading.Content = heading.ToString();
                                                this.backW.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
                                                this.intercept.RenderTransform = new RotateTransform(bearing - my_heading);
                                                this.intercept_elevation.RenderTransform = new RotateTransform(90 - elevation);

                                                enemy_name.Visibility = Visibility.Visible;
                                                if (enemi_name != null)
                                                    enemy_name.Content = enemi_name;
                                                
                                            }));
                                        }
                                        else
                                        {    
                                            if (warning_enemy)
                                            {
                                                warning_enemy = false;
                                                this.Dispatcher.BeginInvoke((Action)(() =>
                                                {
                                                    label_distance.Content = "0";
                                                    label_altidude.Content = "0";

                                                    label_count.Content = "0";
                                                    LabelBearing.Content = "0";

                                                    label_heading.Content = "0";
                                                    this.backW.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                                                    enemy_name.Visibility = Visibility.Hidden;
                                                }));
                                            }
                                            
                                        }
                                            
                                        Thread.Sleep(10);    
                                        
                                        this.Dispatcher.BeginInvoke((Action)(() =>
                                            {                                                
                                                label_time.Content = my_current_time.ToString();
                                            }));
                                        
                                        string output = words[0].Substring(words[0].IndexOf('#') + 1);
                                        my_current_time = float.Parse(output);
                                        this.Dispatcher.BeginInvoke((Action)(() =>
                                        {
                                            label_time.Content = my_current_time.ToString();
                                        }));

                                        if ((gps_active)&&(warning_enemy==false)&&(drive_home))
                                        {
                                            float dest_latitude= my_latitude, dest_longityde= my_longitude;
                                            if (drive_home)
                                            {
                                                dest_latitude = home_latitude;
                                                dest_longityde = home_longitude;
                                            }
                                           
                                            double brng = Bearing_fromA_toB(my_latitude, my_longitude, dest_latitude, dest_longityde);

                                            var sCoord = new System.Device.Location.GeoCoordinate(my_latitude, my_longitude);
                                            var eCoord = new System.Device.Location.GeoCoordinate(dest_latitude, dest_longityde);
                                            double dist = Math.Round(sCoord.GetDistanceTo(eCoord) / 1000);
                                            this.Dispatcher.BeginInvoke((Action)(() =>
                                            {
                                                label_distance.Content = dist.ToString();
                                                label_heading.Content = brng.ToString();
                                            }));
                                        } 
                                    }
                                    catch 
                                    { 
                                        Console.WriteLine("Invalid Time Frame");
                                    }
                                }
                                else
                                {// this is an update line
                                    if (words.Length > 4)
                                    {
                                        if (string_equal(words[4],"Type=Air+FixedWing"))// add nouvel objet
                                        {
                                            if ((words[0]!=null)&& (words[3] != null)&& (words[5] != null))
                                            {
                                                // check if its me !
                                                if (words[3].IndexOf("gameid") > 0)
                                                {
                                                    my_id = words[0];
                                                    my_coalition = words[5];  
                                                }
                                                else
                                                {
                                                    try
                                                    {
                                                        var t = int.Parse(words[0], System.Globalization.NumberStyles.HexNumber);

                                                        parts[t] = index_objet;
                                                        objets.Add(new Objet()
                                                        {
                                                            ObjetName = words[3].Split('=').Last(),
                                                            ObjetType = 0,
                                                            ObjetId = t,
                                                            Coalition = words[5],
                                                            update_time = my_current_time
                                                        }); 
                                                        index_objet++;
                                                    }
                                                    catch
                                                    {
                                                        Console.WriteLine("exeption add objet");
                                                    }                                                    
                                                }
                                            }
                                        }                                        
                                    }
                                    else
                                    {
                                        if (words.Length > 2)
                                        {
                                            if (string_equal(words[0],my_id))// read my position
                                            {
                                                try
                                                {
                                                    if((words[1]!=null)&&(words[2]!=null))
                                                    {
                                                        // lit la position seulement words[1]T=39.436628204|45.101046604|48.34|||78.8
                                                        string output = words[1].Substring(words[1].IndexOf('=') + 1);

                                                        string[] position_string = output.Split('|');
                                                    
                                                        var tp = words[2].Split('=').Last(); // read AGL

                                                        if (tp != null)
                                                            my_altitude = float.Parse(tp);
                                                        if (position_string[1] != null)
                                                            my_latitude = float.Parse(position_string[1]);
                                                        if (position_string[0] != null)
                                                            my_longitude = float.Parse(position_string[0]);
                                                        if (position_string[5] != null)
                                                        { 
                                                            my_heading = float.Parse(position_string[5]);
                                                        }
                                                    }                                                    
                                                }
                                                catch
                                                {
                                                    Console.WriteLine("Exeption error reading my position");
                                                }
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    var code = int.Parse(words[0], System.Globalization.NumberStyles.HexNumber);

                                                    if (parts.ContainsKey(code))
                                                    { 
                                                        string output = words[1].Substring(words[1].IndexOf('=') + 1);
                                                        string[] position_string = output.Split('|');

                                                        var t = int.Parse(words[0], System.Globalization.NumberStyles.HexNumber);
                                                        
                                                        var i = parts[t];

                                                        if (position_string[1]!=null)
                                                        {
                                                            var latitude = float.Parse(position_string[1]);
                                                            objets[i].latitude = (latitude);
                                                        }
                                                        if (position_string[0] != null)
                                                        {
                                                            var longitude = float.Parse(position_string[0]);
                                                            objets[i].longitude = (longitude);
                                                        }
                                                        if (position_string[2] != null)
                                                        {
                                                            var altitude = float.Parse(words[2].Split('=').Last());
                                                            objets[i].altitude = (altitude);
                                                        }
                                                        if (position_string[5] != null)
                                                        {
                                                            var heading = float.Parse(position_string[5]); 
                                                            //if (heading < 0) heading += 360;
                                                            objets[i].heading = heading;
                                                           
                                                        }
                                                        objets[i].update_time = my_current_time;

                                                        var sCoord = new System.Device.Location.GeoCoordinate(my_latitude, my_longitude);
                                                        var eCoord = new System.Device.Location.GeoCoordinate(objets[i].latitude, objets[i].longitude);

                                                        objets[i].distance = sCoord.GetDistanceTo(eCoord);

                                                        objets[i].bearing = Bearing_fromA_toB(my_latitude, my_longitude, objets[i].latitude, objets[i].longitude);

                                                        /*double teta1 = DegreesToRadians(my_latitude);
                                                        double teta2 = DegreesToRadians(objets[i].latitude);
                                                        //double delta1 = DegreesToRadians(latitude - my_latitude);
                                                        double delta2 = DegreesToRadians(objets[i].longitude - my_longitude);

                                                        //==================Heading Formula Calculation================//

                                                        double y = Math.Sin(delta2) * Math.Cos(teta2);
                                                        double x = Math.Cos(teta1) * Math.Sin(teta2) - Math.Sin(teta1) * Math.Cos(teta2) * Math.Cos(delta2);
                                                        double brng = Math.Atan2(y, x);
                                                        brng = brng * 180.00 / Math.PI;// radians to degrees
                                                        brng = (((int)brng + 360) % 360);

                                                        // Math.Atan2 can return negative value, 0 <= output value < 2*PI expected 
                                                        objets[i].bearing = brng;         */                                
                                                    }
                                                }
                                                catch
                                                {
                                                    Console.WriteLine("exepction reading other plane position");
                                                }
                                            }
                                        }
                                    }

                                }
                            }                             
                        }
                        Textfile.Close();
                    }
                }
            }            
        }

        private bool string_equal(string v1, string v2)
        {
            if ((v1==null)||(v2==null))
                    return false;

            if (v1 == v2)
                return true;

            return false;            
        }

        private double DegreesToRadians(float angle)
        {
            return angle * Math.PI / 180.00;
            
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            this.Top = 0;
            this.Left = 0;

           
            string file = "C://Program Files (x86)//Steam//steamapps//common//IL-2 Sturmovik Battle of Stalingrad//data//Tracks//" + pathText.Text+".acmi";
            // By using StreamReader 
            

            if (File.Exists(file))
            {
                // Reads file line by line 

                fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                
                stopButton.IsEnabled = true;
                startButton.IsEnabled = false;
                reading = true;

                pathText.Visibility = Visibility.Hidden;

            }
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            reading = false;
            stopButton.IsEnabled = false;
            startButton.IsEnabled = true;
            pathText.Visibility = Visibility.Visible;

        }
       

        private void button_clear_Click(object sender, RoutedEventArgs e)
        {
            label_distance.Content = "0";
            label_altidude.Content = "0";
            
            label_count.Content = "0";
            LabelBearing.Content = "0";
            
            label_heading.Content = "0";
            this.backW.Fill = new SolidColorBrush(Color.FromArgb(255, 255,255, 255));
            this.intercept.RenderTransform = new RotateTransform(0);
            this.intercept_elevation.RenderTransform = new RotateTransform(90);

            enemy_name.Visibility = Visibility.Hidden;


        }

        bool gps_active = false;
        float location_latitude=0;
        float location_longitude=0;

        public bool drive_home = false;
        private void gps_button_Click(object sender, RoutedEventArgs e)
        {
            if(gps_active)
            {
                gps_button.Content = "S";
                gps_active = false;
                gps_button.Background = new SolidColorBrush(Color.FromArgb(255, 0xDD, 0xDD, 0xDD));

            }
            else
            {
                gps_button.Content = "G"; 
                gps_active = true;
                gps_button.Background = new SolidColorBrush(Color.FromArgb(255, 0x4B, 0xDD, 0xDD));

            }

        }

        private void Home_button_Click_1(object sender, RoutedEventArgs e)
        {
            switch (Home_button.Content)
            {
                case "H":
                    Home_button.Content = "S";
                    Home_button.Background = new SolidColorBrush(Color.FromArgb(255, 0xDD, 0xDD, 0xDD));

                    home_latitude = my_latitude;
                    home_longitude = my_longitude;                    
                    break;
                case "S":
                    Home_button.Content = "G";
                    drive_home = false;
                    break;
                case "G":
                    Home_button.Content = "S";
                    drive_home = true;
                     
                    break;
            } 
        }
    }
}
