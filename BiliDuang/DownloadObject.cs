﻿using BiliDuang.tools;
using BiliDuang.VideoClass;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BiliDuang
{

    public class DownloadObject
    {
        /**
         * 
         * 正数正常,负数不正常
         *  0 下载未开始 
         *  1 下载已暂停
         *  2 下载被手动结束
         *  5 下载开始
         *  
         * -1 链接获取错误
         * -2 下载文件错误
         * -3 速度获取报错
         * -4 下载完成报错
         * -5 合并视频报错
         * 
         * 66 下载完成
         */
        public int status = 0;
        public string message;
        private bool wcusing = false;
        public bool handpause;
        private bool cancel = false;
        public int type = 0;// 0 - InnerDownloadObject  1 - Aria2cDownloadObject

        //基本信息
        public string saveto;
        public string aid;
        public string cid;
        public string name
        {
            get => "[" + VideoQuality.Name(quality) + "] " + avname;
            set
            {
                return;
            }
        }
        private string _avname;
        public string avname
        {
            set
            {
                value = value.Replace("\\", "＼");
                value = value.Replace("/", "／");
                value = value.Replace(":", "：");
                value = value.Replace("?", "？");
                value = value.Replace("\"", "＂");
                value = value.Replace("<", "＜");
                value = value.Replace(">", "＞");
                value = value.Replace("|", "｜");
                _avname = value;
            }
            get => _avname;
        }
        public int blocknum = 0;
        public WebClient wc = new WebClient();


        public List<DownloadUrl> urls = new List<DownloadUrl>();
        public int quality;
        private bool single = false;
        private readonly Stopwatch sw = new Stopwatch();

        public int progress;//进度用于进度条
        public double speed;
        private Process ariap;

        public DownloadObject(string aid, string cid, int quality, string saveto, string name, string avname)
        {
            this.aid = aid;
            this.cid = cid;
            this.quality = quality;
            this.saveto = saveto;
            this.name = name;
            this.avname = avname;
            type = Settings.usearia2c ? 1 : 0;
        }

        public void LinkStart()
        {
            if (wcusing)
            {
                return;
            }
            else
            {
                wc = new WebClient();
            }

            status = 0;
            if (urls.Count == 0)
            {
                if (!GetDownloadUrls())
                {
                    status = -1;
                    return;
                }
            }

            if (urls.Count == 1)
            {
                single = true;
            }

            if (type == 0)
            {
                //InnerDownload
                try
                {
                    wc.Headers.Add("Cookie", User.cookie);
                    wc.DownloadFileCompleted += new AsyncCompletedEventHandler(CompletedHandle);
                    wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged);
                    //wc.Accept = "*/*";
                    //wc.Referer = "https://bilibili.com/";
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.13; rv:56.0) Gecko/20100101 Firefox/56.0");
                    wc.Headers.Add("Origin", "https://www.bilibili.com");
                    wc.Headers.Add("Referer", "https://www.bilibili.com");
                    Uri uri = new Uri(urls[blocknum].url);
                    status = 5;
                    if (!single)
                    {
                        Directory.CreateDirectory(saveto + "/" + avname);
                        message = "开始下载";
                        //当前暂不支持断点续传,于是我们便把之前的文件删掉吧
                        if (File.Exists(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type))
                        {
                            FileInfo fi = new FileInfo(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                            if (fi.Length == urls[blocknum].size)
                            {
                                Completed(true, "文件已经存在且大小正确");
                                return;
                            }
                            File.Delete(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);

                        }
                        Console.WriteLine("Creating Download url: " + urls[blocknum].url + " to " + saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                        sw.Start();
                        wcusing = true;
                        wc.DownloadFileAsync(uri, saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                    }
                    else
                    {
                        message = "开始下载";
                        if (File.Exists(saveto + "/" + avname + "." + urls[blocknum].type))
                        {
                            FileInfo fi = new FileInfo(saveto + "/" + avname + "." + urls[blocknum].type);
                            if (fi.Length == urls[blocknum].size)
                            {
                                Completed(true, "文件已经存在且大小正确");
                                return;
                            }
                            File.Delete(saveto + "/" + avname + "." + urls[blocknum].type);
                        }
                        File.Delete(saveto + "/" + avname + " - " + name + "_" + blocknum.ToString() + "." + urls[blocknum].type);
                        Console.WriteLine("Creating Download url: " + urls[blocknum].url + " to " + saveto + "/" + avname + " - " + name + "_" + blocknum.ToString() + "." + urls[blocknum].type);
                        sw.Start();
                        wcusing = true;
                        wc.DownloadFileAsync(uri, saveto + "/" + avname + "." + urls[blocknum].type);
                    }
                }

                catch (WebException we)
                {
                    status = -2;
                    message = "文件分片" + blocknum.ToString() + "下载错误: " + we.Message;
                    wcusing = false;
                }
                catch (System.NotSupportedException e)
                {
                    status = -2;
                    message = e.Message;
                    wcusing = false;

                }
                catch (Exception e)
                {
                    wcusing = false;
                    status = -2;
                    message = "分片" + blocknum.ToString() + "下载错误: " + e.Message;
                }
            }
            else
            {
                //Aria2c
                //{"jsonrpc":"2.0","method":"aria2.addUri","id":"这是啥","params":["token:TOKEN",["链接"],{"dir":"D:\\Myself\\Downloads"}]}
                if (!single)
                {
                    Directory.CreateDirectory(saveto + "/" + avname);
                    message = "开始下载";
                    //当前暂不支持断点续传,于是我们便把之前的文件删掉吧
                    if (File.Exists(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type))
                    {
                        FileInfo fi = new FileInfo(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                        if (fi.Length == urls[blocknum].size)
                        {
                            Completed(true, "文件已经存在且大小正确");
                            return;
                        }
                        File.Delete(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                    }
                    Console.WriteLine("Creating Download url by aria2c: " + urls[blocknum].url + " to " + saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);

                    DownloadFileByAria2(urls[blocknum].url, saveto + "/" + avname, blocknum.ToString() + "." + urls[blocknum].type);
                }
                else
                {
                    message = "开始下载";
                    if (File.Exists(saveto + "/" + avname + "." + urls[blocknum].type))
                    {
                        FileInfo fi = new FileInfo(saveto + "/" + avname + "." + urls[blocknum].type);
                        if (fi.Length == urls[blocknum].size)
                        {
                            Completed(true, "文件已经存在且大小正确");
                            return;
                        }
                        File.Delete(saveto + "/" + avname + "." + urls[blocknum].type);
                    }
                    File.Delete(saveto + "/" + avname + " - " + name + "_" + blocknum.ToString() + "." + urls[blocknum].type);
                    Console.WriteLine("Creating Download url: " + urls[blocknum].url + " to " + saveto + "/" + avname + " - " + name + "_" + blocknum.ToString() + "." + urls[blocknum].type);
                    DownloadFileByAria2(urls[blocknum].url, saveto, avname + "." + urls[blocknum].type);
                }
            }

        }

        internal void Resume()
        {
            LinkStart();
        }

        internal void Cancel()
        {
            if (type == 0)
            {
                wcusing = false;
                cancel = true;
                wc.CancelAsync();
                wc.Dispose();
            }
            else
            {
                ariap.Kill();
            }

        }

        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            try
            {
                // 显示下载速度
                speed = Convert.ToDouble(e.BytesReceived) / sw.Elapsed.TotalSeconds;

                if (urls[blocknum].size == -1)
                {
                    urls[blocknum].size = e.TotalBytesToReceive;
                }

                // 进度条
                progress = (e.ProgressPercentage / urls.Count) + (blocknum * 100 / urls.Count);

                // 下载了多少 还剩余多少
                //labelDownloaded.Text = (Convert.ToDouble(e.BytesReceived) / 1024 / 1024).ToString("0.00") + " Mb's" + "  /  " + (Convert.ToDouble(e.TotalBytesToReceive) / 1024 / 1024).ToString("0.00") + " Mb's";
                //正在下载区块 {0}/{5}: {1}/{2}  {3}% 速度:{4}/s <{6}>

                message = string.Format("正在下载区块{0}/{1}: {2}/{3} {4}% 速度:{5}/s <{6}>", blocknum + 1, urls.Count, byteConvert.GetSize(e.BytesReceived), byteConvert.GetSize(e.TotalBytesToReceive), progress.ToString(), byteConvert.GetSize(speed), "NaN");
            }
            catch (Exception ex)
            {
                status = -3;
                message = "进度获取错误" + ex.Message;
            }

        }

        private void CompletedHandle(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                Completed(false, e.Error.Message);

            }
            else
            {
                Completed(true, "下载完成");
            }
        }

        private void Completed(bool complete, string msg)
        {
            wc.Dispose();
            wcusing = false;
            sw.Reset();
            if (status == 1)
            {
                return;
            }

            if (complete != true)
            {
                if (!cancel)
                {
                    status = -4;
                    message = "下载未完成,可能是网络中断,正在重试";
                    Console.WriteLine("下载出错," + msg);
                    LinkStart();
                    return;
                }
            }
            else
            {
                message = "下载完成:" + saveto + avname + " - " + name + "." + urls[blocknum].type;
                if (blocknum != urls.Count - 1)
                {
                    if (single)
                    {
                        FileInfo fi = new FileInfo(saveto + "/" + avname + "." + urls[blocknum].type);
                        Console.WriteLine("Download Complete! Downloaded Size: " + fi.Length.ToString() + " Server Size: " + urls[blocknum].size.ToString());
                        if (urls[blocknum].size != -1 && fi.Length != urls[blocknum].size)
                        {
                            Console.WriteLine("Size Error, Try Download Again");
                            message = "区块" + (blocknum + 1).ToString() + "下载出错,正在重试";
                            LinkStart();
                            return;
                        }
                    }
                    else
                    {
                        FileInfo fi = new FileInfo(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                        Console.WriteLine("Download Complete! Downloaded Size: " + fi.Length.ToString() + " Server Size: " + urls[blocknum].size.ToString());
                        if (urls[blocknum].size != -1 && fi.Length != urls[blocknum].size)
                        {
                            Console.WriteLine("Size Error, Try Download Again");
                            message = "区块" + (blocknum + 1).ToString() + "下载出错,正在重试";
                            LinkStart();
                            return;
                        }
                    }
                    blocknum++;
                    LinkStart();
                    return;
                }
                else if (!single)
                {

                    FileInfo fi = new FileInfo(saveto + "/" + avname + "/" + blocknum.ToString() + "." + urls[blocknum].type);
                    Console.WriteLine("Download Complete! Downloaded Size: " + fi.Length.ToString() + " Server Size: " + urls[blocknum].size.ToString());
                    if (urls[blocknum].size != -1 && fi.Length != urls[blocknum].size)
                    {
                        Console.WriteLine("Size Error, Try Download Again");
                        message = "区块" + (blocknum + 1).ToString() + "下载出错,正在重试";
                        LinkStart();
                        return;
                    }
                    MergeVideo();
                }
                else
                {
                    status = 66;
                    message = "下载完成!";
                }
            }
        }

        private void MergeVideo()
        {
            //Goodbye FFMPEG
            message = "合并分块到一个视频文件中";
            if (urls[0].type == "mp4")
            {
                string fc = "";
                List<string> filenames = new List<string>();
                for (int i = 0; i < urls.Count; i++)
                {
                    filenames.Add(saveto + "/" + avname + "/" + i + "." + urls[i].type);
                }

                foreach (string file in filenames)
                {
                    fc += string.Format("-add \"{0}\" ", file);
                }
                string argu = string.Format("{0}-new \"{1}\"", fc, (saveto + "/" + avname + "." + urls[0].type));
                Process exep = new Process();
                exep.StartInfo.CreateNoWindow = true;
                exep.StartInfo.Arguments = argu;
                //不使用操作系统使用的shell启动进程
                exep.StartInfo.UseShellExecute = false;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    exep.StartInfo.FileName = Environment.CurrentDirectory + "/tools/mp4box.exe";
                }
                else
                {
                    exep.StartInfo.FileName = "mp4box";
                }

                exep.Start();
                exep.WaitForExit();//关键，等待外部程序退出后才能往下执行
                if (File.Exists(saveto + "/" + avname + "." + urls[0].type))
                {
                    Directory.Delete(saveto + "/" + avname, true);
                    status = 66;
                    message = "下载完成!";
                }
                else
                {
                    status = -5;
                    message = "视频合并出错,下载缓存暂未删除";
                }

            }
            else if (urls[0].type == "flv")
            {
                List<string> filenames = new List<string>();
                for (int i = 0; i < urls.Count; i++)
                {
                    filenames.Add(saveto + "/" + avname + "/" + i + "." + urls[i].type);
                }
                if (FlvMerger.StartMerge(filenames, (saveto + "/" + avname + "." + urls[0].type)))
                {
                    if (File.Exists(saveto + "/" + avname + "." + urls[0].type))
                    {
                        Directory.Delete(saveto + "/" + avname, true);
                        status = 66;
                        message = "下载完成!";
                    }
                    else
                    {
                        status = -5;
                        message = "视频合并出错,下载缓存暂未删除";
                    }
                }
                else
                {
                    status = -5;
                    message = "视频合并出错,下载缓存暂未删除";
                }

            }

        }

        public void Pause()
        {
            if (type == 0)
            {
                status = 1;
                message = "停止中";
                wcusing = false;
                wc.CancelAsync();
                wc.Dispose();
            }
            else
            {
                if (ariap != null)
                {
                    ariap.Kill();
                }

                status = 1;
                message = "停止中";
            }

        }

        private bool GetDownloadUrls()
        {
            if (quality < VideoQuality.Q4K)
            {

                //下载链接api为 https://api.bilibili.com/x/player/playurl?avid=44743619&cid=78328965&qn=32 cid为上面获取到的 avid为输入的av号 qn为视频质量
                //https://www.biliplus.com/BPplayurl.php?otype=json&cid=29892777&module=bangumi&qn=16
                WebClient MyWebClient = new WebClient
                {
                    Credentials = CredentialCache.DefaultCredentials//获取或设置用于向Internet资源的请求进行身份验证的网络凭据            
                };
                string callback = "";
                try
                {
                    switch (Settings.useapi)
                    {
                        case 0:
                            MyWebClient.Headers.Add("Cookie", User.cookie);
                            callback = Encoding.UTF8.GetString(MyWebClient.DownloadData(string.Format("https://api.bilibili.com/x/player/playurl?avid={0}&cid={1}&qn={2}", aid, cid, quality.ToString()))); //如果获取网站页面采用的是UTF-8，则使用这句
                            break;
                        case 1:
                            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; //加上这一句
                            callback = Encoding.UTF8.GetString(MyWebClient.DownloadData(string.Format("https://www.biliplus.com/BPplayurl.php?otype=json&module=bangumi&avid={0}&cid={1}&qn={2}", aid, cid, quality.ToString()))); //如果获取网站页面采用的是UTF-8，则使用这句
                            break;
                        case 2:
                            //force_host=0&&npcybs=0
                            MyWebClient.Headers.Add("Cookie", User.cookie);
                            string api = string.Format("/x/tv/ugc/playurl?avid={0}&cid={1}&qn={2}&type=&otype=json&device=android&platform=android&mobi_app=android_tv_yst&build=102801&fnver=0&fnval=80", aid, cid, quality.ToString());
                            callback = Encoding.UTF8.GetString(MyWebClient.DownloadData("https://api.bilibili.com" + api)); //如果获取网站页面采用的是UTF-8，则使用这句
                            break;
                    }
                }
                catch (WebException e)
                {
                    Dialog.Show("无法下载," + e.Message);
                    return false;
                }
                MyWebClient.Dispose();
                switch (Settings.useapi)
                {
                    case 0:
                        JSONCallback.Player.Player player = JsonConvert.DeserializeObject<JSONCallback.Player.Player>(callback);
                        if (player.code == -404)
                        {
                            Dialog.Show(string.Format("无法下载 {0}({1}), 该视频需要大会员登录下载,请先登录", player.code, player.message), "获取错误");
                            return false;
                        }
                        else if (player.code != 0)
                        {
                            Dialog.Show(string.Format("无法下载 {0}({1})", player.code, player.message), "获取错误");
                            return false;
                        }
                        if (!player.data.accept_quality.Contains(quality))
                        {
                            Console.WriteLine(string.Format("没有指定的画质 {0} ,最高画质为 {1}, 自动下载最高画质{1}", VideoQuality.Name(quality), VideoQuality.Name(player.data.accept_quality[0])));
                            quality = player.data.accept_quality[0];
                            return GetDownloadUrls();//我太懒了,直接递归吧
                        }
                        foreach (JSONCallback.Player.DurlItem Item in player.data.durl)
                        {
                            DownloadUrl du = new DownloadUrl
                            {
                                url = Item.url,
                                size = Item.size
                            };
                            urls.Add(du);
                        }
                        return true;
                        break;
                    case 1:
                        if (callback == "")
                        {
                            Dialog.Show(string.Format("使用BiliPlus API出错!"), "获取错误");
                            return false;
                        }
                        JSONCallback.BiliPlus.Player playerb = JsonConvert.DeserializeObject<JSONCallback.BiliPlus.Player>(callback);
                        if (!playerb.accept_quality.Contains(quality))
                        {
                            Console.WriteLine(string.Format("没有指定的画质 {0} ,最高画质为 {1}, 自动下载最高画质{1}", VideoQuality.Name(quality), VideoQuality.Name(playerb.accept_quality[0])));
                            quality = playerb.accept_quality[0];
                            return GetDownloadUrls();//我太懒了,直接递归吧
                        }
                        foreach (JSONCallback.BiliPlus.DurlItem Item in playerb.durl)
                        {
                            DownloadUrl du = new DownloadUrl
                            {
                                url = Item.url,
                                size = Item.size
                            };
                            urls.Add(du);
                        }
                        return true;
                        break;
                    case 2:

                        JSONCallback.FourKPlayer.Data playertv = JsonConvert.DeserializeObject<JSONCallback.FourKPlayer.Data>(callback);
                        if (playertv.code != 0)
                        {
                            return false;
                        }

                        if (!playertv.accept_quality.Contains(quality))
                        {
                            Console.WriteLine(string.Format("没有指定的画质 {0} ,最高画质为 {1}, 自动下载最高画质{1}", VideoQuality.Name(quality), VideoQuality.Name(playertv.accept_quality[0])));
                            quality = playertv.accept_quality[0];
                            return GetDownloadUrls();//我太懒了,直接递归吧
                        }
                        foreach (JSONCallback.FourKPlayer.VideoItem Item in playertv.dash.video)
                        {
                            if (Item.id != quality)
                            {
                                continue;
                            }

                            DownloadUrl du = new DownloadUrl
                            {
                                type = "mp4",
                                url = Item.base_url,
                                size = -1//暂不支持检测大小
                            };
                            urls.Add(du);
                            du = new DownloadUrl
                            {
                                type = "mp3",
                                url = playertv.dash.audio[0].base_url,
                                size = -1//暂不支持检测大小
                            };
                            urls.Add(du);
                            return true;
                        }
                        return false;
                        break;
                    default:
                        return false;
                }

            }
            else
            {
                string callback = Other.GetHtml("https://www.bilibili.com/video/av" + aid, true);
                string json = Other.TextGetCenter("window.__playinfo__=", "</script>", callback);
                try
                {
                    JSONCallback.FourKPlayer.FourKPlayer player = JsonConvert.DeserializeObject<JSONCallback.FourKPlayer.FourKPlayer>(json);
                    if (player.code == -404)
                    {
                        Dialog.Show(string.Format("无法下载 {0}({1}), 该视频需要大会员登录下载,请先登录", player.code, player.message), "获取错误");
                        return false;
                    }
                    else if (player.code != 0)
                    {
                        Dialog.Show(string.Format("无法下载 {0}({1})", player.code, player.message), "获取错误");
                        return false;
                    }
                    if (!player.data.accept_quality.Contains(quality))
                    {
                        Console.WriteLine(string.Format("没有指定的画质 {0} ,最高画质为 {1}, 自动下载最高画质{1}", VideoQuality.Name(quality), VideoQuality.Name(player.data.accept_quality[0])));
                        quality = player.data.accept_quality[0];
                        return GetDownloadUrls();//我太懒了,直接递归吧
                    }
                    foreach (JSONCallback.FourKPlayer.VideoItem Item in player.data.dash.video)
                    {
                        if (Item.id != quality)
                        {
                            continue;
                        }

                        DownloadUrl du = new DownloadUrl
                        {
                            type = Item.mimeType.Replace("video/", ""),
                            url = Item.baseUrl,
                            size = -1//暂不支持检测大小
                        };
                        urls.Add(du);
                        du = new DownloadUrl
                        {
                            type = "mp3",
                            url = player.data.dash.audio[1].baseUrl,
                            size = -1//暂不支持检测大小
                        };
                        urls.Add(du);
                        return true;
                    }
                    return false;
                }
                catch (Exception)
                {
                    Console.WriteLine("4K获取出错,正在尝试降级后重试.");
                    quality = VideoQuality.Q1080P60;
                    return GetDownloadUrls();
                }

            }
        }

        #region Aria2c下载
        public void DownloadFileByAria2(string url, string directory, string filename)
        {
            string command = Settings.aria2cargument + " --user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:84.0) Gecko/20100101 Firefox/84.0\" --header=\"Origin: https://www.bilibili.com\" --header=\"Referer: https://www.bilibili.com\" -d \"" + directory + "\" -o \"" + filename + "\" " + url;
            ariap = new Process();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                ExecuteAria2c(ariap, command, (s, e) => ShowInfo(e.Data));
            }
            else
            {
                ExecuteAria2c(ariap, command, (s, e) => ShowInfo(e.Data));
            }
        }
        private void ShowInfo(string outputstr)
        {
            if (string.IsNullOrWhiteSpace(outputstr))
            {
                return;
            }

            if (outputstr.Contains("Downloading"))
            {
                status = 5;
            }

            if (outputstr.Contains("Redirecting")) { message = "正在获取真实下载链接"; status = 5; return; }
            if (outputstr.Contains("(OK)")) { status = 66; Completed(true, "下载完成"); }
            Regex regex = new Regex("\\[#\\S* (\\S*)/(\\S*)\\(([0-9]\\d{0,1})%\\) CN:\\S* DL:(\\S*) ETA:(\\S*)]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            message = regex.Replace(outputstr, "Aria2c 已下载: $1 / $2 ($3%)  速度: $4/s 剩余时间: $5");
        }

        private void ExecuteAria2c(Process p, string argument, DataReceivedEventHandler output)
        {
            p.StartInfo.FileName = (Environment.OSVersion.Platform == PlatformID.Win32NT && File.Exists("aria2c")) ? "tools/aria2c.exe" : "aria2c";
            p.StartInfo.Arguments = argument;

            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;


            p.OutputDataReceived += output;
            p.ErrorDataReceived += output;
            p.Exited += (o, e) => { if (p.ExitCode != 0 || status != 66) { status = -5; } };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }
        #endregion
    }

    public class DownloadUrl
    {
        public string type = "flv";
        public string url;
        public long size;
    }
}