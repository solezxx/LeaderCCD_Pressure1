using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Configuration;
using BingLibrary.hjb.file;
using OxyPlot.Annotations;
using OxyPlot.Legends;

namespace LeaderCCD
{
    public class MainWindowViewModel : INotifyPropertyChanged

    {
        public MainWindowViewModel()
        {
            ModbusTCP = new DXH.Modbus.DXHModbusTCP();
            ModbusTCP.ModbusStateChanged += ModbusTCP_ModbusStateChanged;//Modbus通信状态
            ModbusTCP.ConnectStateChanged += ModbusTCP_ConnectStateChanged;//Modbus连接状态
            PLCStatusChanged += Main_PLCStatusChanged;
            PlcIpAddress = ConfigurationManager.AppSettings["PlcIpAddress"];
            StartReadPLC();
            PlotIni();
            GetSpline();
        }

        private void Main_PLCStatusChanged(object sender, string e)
        {
            DoPLCStatusChanged(e);
        }
        /// <summary>
        /// 普通保存计次，0就是刚开始
        /// </summary>
        private int times;
        /// <summary>
        /// 和max都是最大值，但是这是为了防止保存有时间延迟导致数据不一样
        /// </summary>
        private double x;

        private bool test;
        public int minPress,maxPress;
        private void DoPLCStatusChanged(string e)
        {
            if (e == "Start")
            {
                test = Convert.ToInt32(ConfigurationManager.AppSettings["TimesBox1"]) == 1;
                minPress = Convert.ToInt32(ConfigurationManager.AppSettings["PreBoxMin"] );
                maxPress = Convert.ToInt32(ConfigurationManager.AppSettings["PreBox"]);
                times = 0;
                second = false;
                max = 0;
                maxxx = 0;
                minnn= 1000000;
                ModbusTCP.ModbusWrite(1, 15, 350, new int[] { 0 });
                lineSeries.Points.Clear();
                ModelRes.InvalidatePlot(true);
            }

            if (e == "Position")
            {
                ModbusTCP.ModbusWrite(1, 15, 351, new int[] { 0 });
                x = max;
                Saveinform(times == 0, x.ToString());
                Getline(times, x);
                LdrLog("第" + (times + 1) + "次，最大值" + x);
                times++;
                max = 0;
            }
        }
        #region 属性


        private bool start;
        public bool Start
        {
            get
            {
                return start;
            }
            set
            {
                if (start != value)
                {
                    start = value;
                    if (start)
                    {
                        PLCStatusChanged?.Invoke(null, "Start");
                    }
                }
            }
        }
        private bool position;
        public bool Position
        {
            get
            {
                return position;
            }
            set
            {
                if (position != value)
                {
                    position = value;
                    if (position)
                    {
                        PLCStatusChanged?.Invoke(null, "Position");
                    }
                }
            }
        }

        private double xyzposition;
        public double xyzPosition
        {
            get
            {
                return xyzposition;
            }
            set
            {
                xyzposition = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Position"));
            }
        }
        private double pressure;
        public double Pressure  
        {
            get
            {
                return pressure;
            }
            set
            {
                pressure = value;
            }
        }
        private string plcstate = "PLC未连接";
        public string PLCState
        {
            get
            {
                return plcstate;
            }
            set
            {
                plcstate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PLCState"));
            }
        }
        public Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        private string plcipaddress;
        public string PlcIpAddress
        {
            get
            {
                return plcipaddress;
            }
            set
            {
                plcipaddress = value;
                cfa.AppSettings.Settings["PlcIpAddress"].Value = value;
                cfa.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                ModbusTCP.RemoteIPAddress = value;
                if (ModbusTCP.ModbusState)
                    ModbusTCP.Close();
                ModbusTCP.StartConnect();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PlcIpAddress"));
            }
        }

        #endregion
        /// <summary>
        /// 记录第二张表的最大值
        /// </summary>
        double max;
        static bool mHasStartPLC = false;
        public DXH.Modbus.DXHModbusTCP ModbusTCP;
        public static event EventHandler<string> PLCStatusChanged;
        private async void StartReadPLC()
        {
            if (!mHasStartPLC)
                mHasStartPLC = true;
            else
                return;
            await Task.Run(() =>
            {
                while (mHasStartPLC)
                {
                    try
                    {
                        Thread.Sleep(10);
                        int[] a = ModbusTCP.ModbusRead(1, 3, 88, 2);//读位置
                        int[] b = ModbusTCP.ModbusRead(1, 1, 350, 2);//开始
                        int[] c = ModbusTCP.ModbusRead(1, 3, 610, 1);//读压力
                        if (a == null || b == null||c==null) continue;
                        int p = a[0] + (a[1] << 16);
                        Pressure = c[0];//实时压力
                        xyzPosition = Math.Round(Convert.ToDouble(p), 2) / 100.0;//实时位置
                        Model.Legends[0].LegendTitle = xyzPosition.ToString()+"[mm]";
                        //if (Pressure >= minPress - 1 && Pressure <= minPress + 1)
                        //{
                        //    if(!(xyzPosition>100))
                        //         Model.Axes[1].Minimum = xyzPosition - 0.1;
                        //}
                        //if (Pressure >= maxPress - 1 && Pressure <= maxPress + 1)
                        //{
                        //    Model.Axes[1].Maximum = xyzPosition +100;
                        //}
                        if (test)
                        {
                            if (Pressure >= minPress && Pressure <= maxPress)
                            {
                                SaveinformTest( xyzPosition.ToString());
                            }
                        }
                        if (xyzPosition > max)
                        {
                            max = xyzPosition;
                        }
                        Start = b[0] == 1;
                        Position = b[1] == 1;
                    }
                    catch (Exception e)
                    {
                        // ignored
                    }
                }
            });
        }

        private string oldPath = "";
        /// <summary>
        /// 保存csv
        /// </summary>
        /// <param name="n">是不是新开始</param>
        /// <param name="position">值</param>
        /// <param name="test">是不是保存测试数据，false保存最大值，true保存实时数据</param>
        public async void Saveinform(bool n, string position)
        {
            await Task.Run((() =>
            {
                string filePath = "";

                if (n)
                {
                    filePath = "E:\\保存参数\\正常测试" + ConfigurationManager.AppSettings["TimesBox1"] + "次" + DateTime.Now.ToString("yyyy_MM_dd HH.mm.ss") + ".csv";
                    oldPath = filePath;
                }
                else
                {
                    filePath = oldPath;
                }
                if (!Directory.Exists("E:\\保存参数"))
                {
                    Directory.CreateDirectory("E:\\保存参数");
                }
                try
                {
                    if (!File.Exists(filePath))
                    {
                        string[] heads = { "时间", "位置" };
                        Csvfile.AddNewLine(filePath, heads);
                    }
                    string[] conte = { System.DateTime.Now.ToString(), position };
                    Csvfile.AddNewLine(filePath, conte);
                }
                catch (Exception ex)
                {
                    LdrLog(ex.Message);
                }

            }));
        }
        /// <summary>
        /// 实时保存数据，定义一个second防止无限新建文件
        /// </summary>
        private bool second = false;

        public  void SaveinformTest( string position)
        {
            string filePath = "";
            if (!Directory.Exists("E:\\保存参数"))
            {
                Directory.CreateDirectory("E:\\保存参数");
            }
            if (!second)
            {
                filePath = "E:\\保存参数\\实时保存测试" + ConfigurationManager.AppSettings["TimesBox1"] + "次" + DateTime.Now.ToString("yyyy_MM_dd HH.mm.ss") + ".csv"; ;
                oldPath = filePath;
                second = true;
            }
            else
            {
                filePath = oldPath;
            }
            try
            {
                if (!File.Exists(filePath))
                {
                    string[] heads = { "时间", "位置" };
                    Csvfile.AddNewLine(filePath, heads);
                }
                string[] conte = { System.DateTime.Now.ToString(), position };
                Csvfile.AddNewLine(filePath, conte);
            }
            catch (Exception ex)
            {
                LdrLog(ex.Message);
            }

        }

        private void ModbusTCP_ConnectStateChanged(object sender, string e)
        {

        }

        private void ModbusTCP_ModbusStateChanged(object sender, bool e)
        {
            if (e)
            {
                PLCState = "PLC已连接";
                LdrLog("PLC已连接");
            }
            else
            {
                PLCState = "PLC未连接";
                LdrLog("PLC连接断开");
            }
        }

        #region 实时曲线
        private PlotModel model;
        public PlotModel Model
        {
            get { return model; }
            set
            {
                model = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Model"));
            }
        }
        /// <summary>
        /// 实时曲线
        /// </summary>
        private async void GetSpline()
        {
            Model = new PlotModel
            {
                Title = "实时位置曲线", //图表的Titile
               
            };
            var lineSeries1 = new LineSeries
            {
                Title = "实时位置",
                //MarkerType = MarkerType.Circle,
                //Color = OxyColors.Blue,
                //StrokeThickness = 2,
                //MarkerSize = 3,
                //MarkerStroke = OxyColors.BlueViolet,
            };

            //添加标注线
            //var linePressMaxAnnotation = new OxyPlot.Annotations.LineAnnotation()
            //{
            //    Type = LineAnnotationType.Horizontal,
            //    Color = OxyColors.Red,
            //    LineStyle = LineStyle.Solid,
            //    Y =10,
            //    Text = "Press MAX:" + 10
            //};
            //Model.Annotations.Add(linePressMaxAnnotation);
            //var linePressMinAnnotation = new LineAnnotation()
            //{
            //    Type = LineAnnotationType.Horizontal,
            //    Y =5,
            //    Text = "Press Min:" + 5,
            //    Color = OxyColors.Red,
            //    LineStyle = LineStyle.Solid
            //};
            //Model.Annotations.Add(linePressMinAnnotation);


            //实例图例对象
            Legend legend = new Legend()
            {
                IsLegendVisible = true,//是否可见
                LegendTitleFontSize = 15, //图例标题字体大小
                LegendFontSize = 15, //图例字体大小
                //LegendTitle = "实时位置",//标题
                LegendFont = "Microsoft YaHei UI",//图例字体
                LegendFontWeight = FontWeights.Bold,//图例字形
                LegendTextColor = OxyColors.Automatic,//图例文本颜色
            };
            OxyPlot.Axes.LinearAxis leftAxis = new OxyPlot.Axes.LinearAxis()
            {
                Position = AxisPosition.Left,
                //Minimum = -1,
                //Maximum = 15,
                Title = "位置(mm)",//显示标题内容
                //TitlePosition = 1,//显示标题位置
                //TitleColor = OxyColor.Parse("#d3d3d3"),//显示标题位置
                IsZoomEnabled = false,//坐标轴缩放关闭
                IsPanEnabled = false,//图表缩放功能关闭
                //MajorStep = 1,
                //MinorStep = 1,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
            };
            OxyPlot.Axes.LinearAxis bottomAxis = new DateTimeAxis()
            {
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                IntervalLength = 80,
                IsZoomEnabled = false,//坐标轴缩放关闭
                IsPanEnabled = true,//图表缩放功能关闭
            };
            Model.Axes.Add(bottomAxis);
            Model.Axes.Add(leftAxis);
            Model.Legends.Add(legend);
            Model.Series.Add(lineSeries1);//将线添加到图标的容器中
            
            Random rd = new Random();
            await Task.Run(
                () =>
                {
                    while (true)
                    {
                        lock (lineLock1)
                        {
                            if (Pressure >= minPress && Pressure <= maxPress)
                                lineSeries1.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), xyzPosition));
                            
                            if (lineSeries1.Points.Count > 100)
                            {
                                lineSeries1.Points.RemoveAt(0);
                            }
                            Model.InvalidatePlot(true);
                            Thread.Sleep(100);
                        }
                    }
                });
        }
        #endregion
        #region 结果曲线
        private PlotModel modelRes;
        public PlotModel ModelRes
        {
            get { return modelRes; }
            set
            {
                modelRes = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ModelRes"));
            }
        }
        private Random rm = new Random();
        private LinearAxis leftAxis = new LinearAxis()
        {
            Position = AxisPosition.Left,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            Title = "位置",
            Unit = "mm",
            //MajorStep = 1,
            //MinorStep = 1,
            IsZoomEnabled = false,//坐标轴缩放关闭
            IsPanEnabled = false,//图表缩放功能关闭
        };
        private LinearAxis botAxis = new LinearAxis()
        {
            Position = AxisPosition.Bottom,
            IsZoomEnabled = false,//坐标轴缩放关闭
            IsPanEnabled = true,//图表缩放功能关闭
            Title = "次数",
            //Minimum = 0,
            //MinorStep = 1,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
        };
        private LineSeries lineSeries = new LineSeries()
        {
            Title = "Series 1",
            
            //Color = OxyColors.Blue,
            //MarkerType = MarkerType.Circle
        };
        //Legend legend = new Legend()
        //{
        //    IsLegendVisible = true,//是否可见
        //    LegendTitleFontSize = 10, //图例标题字体大小
        //    LegendFontSize =10, //图例字体大小
        //    LegendTitle = "图例",//标题
        //    LegendFont = "Microsoft YaHei UI",//图例字体
        //    LegendFontWeight = FontWeights.Bold,//图例字形
        //    LegendTextColor = OxyColors.Automatic,//图例文本颜色
        //};//实例图例对象
        LineAnnotation lineAnnotation_max = new OxyPlot.Annotations.LineAnnotation()
        {
            Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
            Color = OxyPlot.OxyColors.Red,
            LineStyle = OxyPlot.LineStyle.Solid,
            FontSize = 15,
            TextVerticalAlignment = VerticalAlignment.Bottom,
            TextLinePosition = 0.5,
        };
        LineAnnotation lineAnnotation_min = new OxyPlot.Annotations.LineAnnotation()
        {
            Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
            Color = OxyPlot.OxyColors.Red,
            LineStyle = OxyPlot.LineStyle.Solid,
            FontSize = 15,
            TextLinePosition = 0.5
        };
        /// <summary>
        /// 初始化压力图表
        /// </summary>
        private void PlotIni()
        {
            ModelRes = new PlotModel()
            {
                Title = "曲线",
                IsLegendVisible = true,

            };
            ModelRes.Axes.Add(leftAxis);
            ModelRes.Axes.Add(botAxis);
            ModelRes.Series.Add(lineSeries);
            ModelRes.Annotations.Add(lineAnnotation_max);
            ModelRes.Annotations.Add(lineAnnotation_min);
            //ModelRes.Legends.Add(legend);
            lineSeries.InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline;

        }
        object lineLock = new object();
        object lineLock1 = new object();
        /// <summary>
        /// 提示线的最大值
        /// </summary>
        private double maxxx=0;
        /// <summary>
        /// 提示线的最小值
        /// </summary>
        private double minnn=1000000;
        /// <summary>
        /// 实时获取，更新曲线
        /// </summary>
        public async void Getline(int times, double y)
        {
            await Task.Run(() =>
            {
                lock (lineLock)
                {
                    lineSeries.Points.Add(new DataPoint(times, y));
                    if(y>maxxx)maxxx=y;
                    if (y<minnn)minnn=y;
                    lineAnnotation_max.Y = maxxx;
                    lineAnnotation_min.Y = minnn;
                    lineAnnotation_max.Text = $"{lineSeries.Title}最大值：{maxxx}";
                    lineAnnotation_min.Text = $"{lineSeries.Title}最小值：{minnn}";
                    //lineSeries.Points.Add(new DataPoint(times, rm.Next(0, 10)));
                    ModelRes.Axes[0].Maximum = maxxx + (maxxx-minnn)/2;
                    ModelRes.Axes[0].Minimum = minnn - (maxxx - minnn) / 2;
                    ModelRes.InvalidatePlot(true);
                }
            });
        }

        #endregion

        #region 日志

        private string logdata;
        public string Logdata
        {
            get
            {
                return logdata;
            }
            set
            {
                logdata = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Logdata"));
            }
        }
        public string Log { get; set; }
        string LogHeader = " -> ";
        object LogLock = new object();
        int LogLine = 0;
        public async void LdrLog(string strtoappend)
        {
            string logstr = CurTime() + LogHeader + strtoappend + Environment.NewLine;
            SaveLog(strtoappend);
            Task task_Log = Task.Run(() =>
            {
                lock (LogLock)//多线程同时输出时会丢失Log
                {
                    LogLine++;
                    if (LogLine > 200)//最多100行。
                    {
                        Log = "";
                        LogLine = 1;
                    }
                    Log = Log + CurTime() + LogHeader + strtoappend + Environment.NewLine;

                }
                Logdata = Log;

            });
            await task_Log;
        }


        public string CurTime()
        {
            return DateTime.Now.ToString();
        }
        public static string FilePath1 = Directory.GetCurrentDirectory() + "/logs/";
        static object LogLock1 = new object();

        public event PropertyChangedEventHandler PropertyChanged;

        public static async void SaveLog(string message)
        {
            Task Task_SaveLog = Task.Run(() =>
            {
                try
                {
                    lock (LogLock1)
                    {
                        if (!Directory.Exists(FilePath1 + DateTime.Now.ToString("yyyy-MM")))
                            Directory.CreateDirectory(FilePath1 + DateTime.Now.ToString("yyyy-MM"));
                        File.AppendAllText(FilePath1 + DateTime.Now.ToString("yyyy-MM") + "/" + DateTime.Now.ToString("yyyy-MM-dd") + ".log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.") + DateTime.Now.Millisecond + "    " + message + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SaveDebugLog:" + ex.Message);
                }
            });
            await Task_SaveLog;
        }

        #endregion
    }
}
