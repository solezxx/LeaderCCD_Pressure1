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
        /// 计次，0就是刚开始
        /// </summary>
        private int times;
        /// <summary>
        /// 和max都是最大值，但是这是为了防止保存有时间延迟导致数据不一样
        /// </summary>
        private double x;

        private bool test;
        private void DoPLCStatusChanged(string e)
        {
            if (e == "Start")
            {
                test = Convert.ToInt32(ConfigurationManager.AppSettings["TimesBox1"]) < 11;
                times = 0;
                max = 0;
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
                        Thread.Sleep(100);
                        int[] a = ModbusTCP.ModbusRead(1, 3, 88, 2);//读位置
                        int[] b = ModbusTCP.ModbusRead(1, 1, 350, 2);//开始
                        if (a == null || b == null) continue;
                        int p = a[0] + (a[1] << 16);
                        xyzPosition = Math.Round(Convert.ToDouble(p), 2) / 100;
                        //if (t)
                        //{
                        //    Saveinform(times == 0, xyzPosition.ToString());
                        //}
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
                    if (!test) filePath = "E:\\保存参数\\正常测试" + ConfigurationManager.AppSettings["TimesBox1"] + "次" + DateTime.Now.ToString("yyyy_MM_dd HH.mm.ss") + ".csv";
                    else
                    {
                        filePath = "E:\\保存参数\\实时保存测试" + ConfigurationManager.AppSettings["TimesBox1"] + "次" + DateTime.Now.ToString("yyyy_MM_dd HH.mm.ss") + ".csv";
                    }
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

                //Subtitle = "直线" //图表的说明
            };
            var lineSeries1 = new LineSeries
            {
                Title = "Series 1",
                //MarkerType = MarkerType.Circle,
                Color = OxyColors.Blue,
                StrokeThickness = 2,
                MarkerSize = 3,
                MarkerStroke = OxyColors.BlueViolet,
            };

            //添加标注线
            //var linePressMaxAnnotation = new OxyPlot.Annotations.LineAnnotation()
            //{
            //    Type = LineAnnotationType.Horizontal,
            //    Color = OxyColors.Red,
            //    LineStyle = LineStyle.Solid,
            //    Y = Convert.ToInt32(Global.Press_Up),
            //    Text = "Press MAX:" + Convert.ToInt32(Global.Press_Up)
            //};
            //Model.Annotations.Add(linePressMaxAnnotation);
            //var linePressMinAnnotation = new LineAnnotation()
            //{
            //    Type = LineAnnotationType.Horizontal,
            //    Y = Convert.ToInt32(Global.Press_Down),
            //    Text = "Press Min:" + Convert.ToInt32(Global.Press_Down),
            //    Color = OxyColors.Red,
            //    LineStyle = LineStyle.Solid
            //};
            //Model.Annotations.Add(linePressMinAnnotation);

            OxyPlot.Axes.LinearAxis leftAxis = new OxyPlot.Axes.LinearAxis()
            {
                Position = AxisPosition.Left,
                //Minimum = -1,
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
            Model.Series.Add(lineSeries1);//将线添加到图标的容器中
            Random rd = new Random();
            await Task.Run(
                () =>
                {
                    while (true)
                    {
                        lock (lineLock1)
                        {
                            lineSeries1.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), xyzPosition));
                            if (lineSeries1.Points.Count > 200)
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
            Title = "位置(mm)",
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
            Minimum = 0,
            //MinorStep = 1,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
        };
        private LineSeries lineSeries = new LineSeries()
        {
            Title = "Series 1",
            Color = OxyColors.Blue,
            //MarkerType = MarkerType.Circle
        };
        /// <summary>
        /// 初始化压力图表
        /// </summary>
        private void PlotIni()
        {
            ModelRes = new PlotModel()
            {
                Title = "曲线",
            };
            ModelRes.Axes.Add(leftAxis);
            ModelRes.Axes.Add(botAxis);
            ModelRes.Series.Add(lineSeries);
        }
        object lineLock = new object();
        object lineLock1 = new object();
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
                    //lineSeries.Points.Add(new DataPoint(times, rm.Next(0, 10)));
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
