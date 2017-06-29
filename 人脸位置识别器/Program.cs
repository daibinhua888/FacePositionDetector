using AForge.Video.DirectShow;
using 人脸位置识别器.JSONs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace 人脸位置识别器
{
    class Program
    {
        static void Main(string[] args)
        {
            FilterInfoCollection videoDevices;
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in videoDevices)
            {
                Console.WriteLine(device.Name);
            }

            var videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
            videoSource.DesiredFrameSize = new Size(320, 240);
            videoSource.DesiredFrameRate = 1;

            videoSource.NewFrame += VideoSource_NewFrame;

            videoSource.Start();

            ThreadStart thd = new ThreadStart(StartDetect);
            Thread th = new Thread(thd);
            th.Start();

            Console.ReadLine();
        }
        
        private static void DetectFacePosition(string imageFile)
        {
            long total_width = GetImageWidth(imageFile);

            var result = POSTfile(imageFile);

            float left = 0;
            float width = 0;
            ResultMessage r = null;
            try
            {
                r = Deserialize<ResultMessage>(result);

                left = r.faces.First().face_rectangle.left;
                width = r.faces.First().face_rectangle.width;
            }
            catch (Exception ex)
            {
                Console.WriteLine(result);
                return;
            }
            var currentPosition = left + (width / 2);
            var basePosition = total_width / 2;

            Console.WriteLine(string.Format("left: {0}, width: {1}", left, width));
            Console.WriteLine(string.Format("currentPosition: {0}", currentPosition));
            Console.WriteLine(string.Format("basePosition: {0}", basePosition));

            OutputTurnAction(currentPosition, basePosition);
        }

        private static void OutputTurnAction(float currentPosition, long basePosition)
        {
            //if (currentPosition < basePosition)
            //    Console.WriteLine("向右转");
            //else if (currentPosition > basePosition)
            //    Console.WriteLine("向左转");
            //else
            //    Console.WriteLine("保持不动");

            //方向反向了
            if (currentPosition < basePosition)
                Console.WriteLine("向左转         <<<<<<<<<<<");
            else if (currentPosition > basePosition)
                Console.WriteLine("向右转         >>>>>>>>>>>");
            else
                Console.WriteLine("保持不动");
        }

        private static string img;
        private static void VideoSource_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            string temp = null;
            using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
            {
                temp = @"D:\monitor\" + DateTime.Now.ToString("yyyyMMddhhmmss.fff") + ".jpg";
                bitmap.Save(temp);
            }

            img = temp;

            Console.WriteLine("SAVED");

            //DetectFacePosition(img);
        }

        private static void StartDetect()
        {
            while (true)
            {
                if (img == null)
                    continue;

                DetectFacePosition(img);

                //Thread.Sleep(1000);
            }
        }

        private static long GetImageWidth(string imageFile)
        {
            using (FileStream fs = new FileStream(imageFile, FileMode.Open, FileAccess.Read))
            {
                System.Drawing.Image image = System.Drawing.Image.FromStream(fs);
                return image.Width;
            }
        }

        public static T Deserialize<T>(string json)
        {
            T obj = Activator.CreateInstance<T>();
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
                return (T)serializer.ReadObject(ms);
            }
        }


        public static string POSTfile(string file)
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");

            //请求
            WebRequest req = WebRequest.Create(@"https://api-cn.faceplusplus.com/facepp/v3/detect");
            req.Method = "POST";
            req.ContentType = "multipart/form-data; boundary=" + boundary;

            //组织表单数据
            StringBuilder sb = new StringBuilder();
            sb.Append("--" + boundary);
            sb.Append("\r\n");
            sb.Append("Content-Disposition: form-data; name=\"api_key\"");
            sb.Append("\r\n\r\n");
            sb.Append("ViYRAk2Y4TRRDB9nBtjPfzHh5QE2YFx0");
            sb.Append("\r\n");

            sb.Append("--" + boundary);
            sb.Append("\r\n");
            sb.Append("Content-Disposition: form-data; name=\"api_secret\"");
            sb.Append("\r\n\r\n");
            sb.Append("s06_J06XY1RfU__AODnH5LIKfu82jHUe");
            sb.Append("\r\n");

            sb.Append("--" + boundary);
            sb.Append("\r\n");
            sb.Append("Content-Disposition: form-data; name=\"image_file\"; filename=\""+ file + "\"");
            sb.Append("\r\n");
            sb.Append("Content-Type: image/pjpeg");
            sb.Append("\r\n\r\n");

            string head = sb.ToString();
            byte[] form_data = Encoding.UTF8.GetBytes(head);
            //结尾
            byte[] foot_data = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

            //文件
            FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            //post总长度
            long length = form_data.Length + fileStream.Length + foot_data.Length;
            req.ContentLength = length;

            var beginTime = DateTime.Now;

            Stream requestStream = req.GetRequestStream();
            //发送表单参数
            requestStream.Write(form_data, 0, form_data.Length);
            //文件内容
            byte[] buffer = new Byte[checked((uint)Math.Min(4096, (int)fileStream.Length))];
            int bytesRead = 0;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                requestStream.Write(buffer, 0, bytesRead);
            //结尾
            requestStream.Write(foot_data, 0, foot_data.Length);
            requestStream.Close();
            fileStream.Close();
            fileStream.Dispose();
            //响应
            WebResponse pos = req.GetResponse();
            StreamReader sr = new StreamReader(pos.GetResponseStream(), Encoding.UTF8);
            string html = sr.ReadToEnd().Trim();
            sr.Close();
            if (pos != null)
            {
                pos.Close();
                pos = null;
            }
            if (req != null)
            {
                req = null;
            }

            var elapsed = DateTime.Now - beginTime;
            Console.WriteLine(string.Format("elapsed: {0}", elapsed.TotalSeconds));

            return html;
        }        
    }
}
