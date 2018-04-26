﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace NetEasePlayer_UWP
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    /// 

    public sealed partial class PlayerPage : Page
    {
        Dictionary<String, List<One>> listMap = new Dictionary<string, List<One>>();
        Live live;
        TappedEventHandler listTapHandler;
        RightTappedEventHandler listRightTapHandler;

        public PlayerPage()
        {
            this.InitializeComponent();
        }
         protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            live = (Live)e.Parameter;
            Play(live.Url);
            InitListTapEvent();

            GetList(new Uri(String.Format("https://hdtv.neu6.edu.cn/{0}.review",
                live.GetSimpleName())));
        }
        private void InitListTapEvent()
        {
            if(listTapHandler == null)
            {
                listTapHandler = new TappedEventHandler((sender, e) =>
                {
                    var o = ((One)((TextBlock)((ListBox)sender).SelectedItem).Tag);
                    Play("http://media2.neu6.edu.cn/review/program-"
                            + o.start.ToString() + "-"
                            + o.end.ToString() + "-" + live.GetSimpleName() + ".m3u8");
                });
                see_back_list.Tapped += listTapHandler;
            }
            if(listRightTapHandler == null)
            {
                listRightTapHandler = new RightTappedEventHandler((sender, e) =>
                {
                    if (((ListBox)sender).SelectedItem != null)
                    {
                        var o = ((One)((TextBlock)((ListBox)sender).SelectedItem).Tag);
                        DownloadShowAsync(o);
                    }
                });
                see_back_list.RightTapped += listRightTapHandler;
            }
        }
        private async Task DownloadShowAsync(One show)
        {
            // http://media2.neu6.edu.cn/review/program-1524520800-1524526860-chchd.m3u8
            var url = String.Format("http://media2.neu6.edu.cn/review/program-{0}-{1}-{2}.m3u8",
                show.start,show.end,live.GetSimpleName());
            Debug.WriteLine(url);
            //下载m3u8文件
            HttpClient httpClient = new HttpClient();
            var response = await httpClient.GetAsync(new Uri(url));
            var res = await response.Content.ReadAsStringAsync();
            Regex re = new Regex(@"(?<url>http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?)");
            MatchCollection mc = re.Matches(res);
            List<String> urlList = new List<string>();
            var tsRe = new Regex(@"(?<ts>[0-9]+\.ts)");
            foreach (Match m in mc)
            {
                var clipUrl = m.Result("${url}");
                urlList.Add(clipUrl);
                var name = tsRe.Match(clipUrl).Result("${ts}");
                res = res.Replace(clipUrl, name);
            }
            res = res.Replace("\n", "\r\n");
            //创建临时文件
            var tempDic = await KnownFolders.VideosLibrary.CreateFolderAsync("temp",
                CreationCollisionOption.GenerateUniqueName);
            //保存本地m3u8
            
            var m3u8 = await tempDic.CreateFileAsync("index.m3u8", CreationCollisionOption.ReplaceExisting);

            using (StorageStreamTransaction transaction = await m3u8.OpenTransactedWriteAsync())
            {
                using (DataWriter dataWriter = new DataWriter(transaction.Stream))
                {
                    dataWriter.WriteString(res);
                    transaction.Stream.Size = await dataWriter.StoreAsync();
                    await transaction.CommitAsync();
                }
            }
            

            //下载视频
            StorageFile destinationFile = await tempDic.CreateFileAsync("whole.ts");
            ulong fileSize = 0;
           
            using (var destinationStream = await destinationFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                foreach (var videoUrl in urlList)
                {
                
                    response = await httpClient.GetAsync(new Uri(videoUrl));
                    var buffer = await response.Content.ReadAsBufferAsync();
                    var stream = await response.Content.ReadAsInputStreamAsync();
                    using (var destinationOutputStream = destinationStream.GetOutputStreamAt(fileSize))
                    {
                        Debug.WriteLine(fileSize);
                        fileSize += (ulong)buffer.AsStream().Length;
                        await RandomAccessStream.CopyAndCloseAsync(stream, destinationOutputStream);
                    }
                }  
            }

            MessageDialog msg = new MessageDialog("下载完毕");
            await msg.ShowAsync();
            

        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            video_player.MediaPlayer.Source = null;
            select_date_combobox.Items.Clear();
            select_date_combobox.SelectedValuePath = "";
        }
        private void Play(String url)
        {
            Debug.WriteLine(url);
            video_player.Source = MediaSource.CreateFromUri( new Uri(url) );
            video_player.Visibility = Visibility.Visible;
        }
        public async Task GetList(Uri uri)
        {

            try
            {
                HttpClient httpClient = new HttpClient();
                var response = await httpClient.GetAsync(uri);
                var buffer = await response.Content.ReadAsBufferAsync();
                //转为UTF-8格式
                DataReader reader = DataReader.FromBuffer(buffer);
                byte[] fileContent = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(fileContent);
                Encoding encoding = Encoding.GetEncoding("UTF-8");
                var str = encoding.GetString(fileContent);
                listMap.Clear();
                //解析json
                JsonObject jsonObject = JsonObject.Parse(str);
                foreach(String date in jsonObject.Keys)
                {
                    JsonArray lives = jsonObject[date].GetArray();
                    List<One> day = new List<One>();
                    foreach(JsonValue one in lives)
                    {
                        
                        var ones = one.GetArray();
                        var o1 = ones.ElementAt(0);
                        long start = Convert.ToInt64(ones.GetStringAt(0));
                        long end = -1;
                        try
                        {
                            end = Convert.ToInt64(ones.GetStringAt(1));

                        }
                        catch
                        {
                            end = Convert.ToInt64(ones.GetNumberAt(1));
                        }
                        String name = ones.GetStringAt(2);
                        day.Add(new One(start,end,name));
                        
                    }
                    listMap.Add(date, day);
                }
                //向页面添加
                var dates = new List<String>(listMap.Keys);
                dates.Sort();
                dates.Reverse();
                String defultComboboxTitle="";
                if(dates.Count() != 0)
                {
                    defultComboboxTitle = dates[0];
                }
                select_date_combobox.Items.Clear();
                foreach (var i in dates)
                {
                    select_date_combobox.Items.Add(i);
                }

                select_date_combobox.SelectionChanged += new SelectionChangedEventHandler((sender,e)=> {
                    see_back_list.Items.Clear();
                    if (select_date_combobox.SelectedItem != null)
                    {
                        foreach (var i in listMap[select_date_combobox.SelectedItem.ToString()])
                        {
                            var t = new TextBlock
                            {
                                Text = i.name,
                                Tag = i
                            };
                            see_back_list.Items.Add(t);
                        }
                    }
                });
                select_date_combobox.SelectedIndex = 0;
               // select_date_combobox.SelectedValuePath = defultComboboxTitle;

                /*
                       "2018-04-05":[

                       [
                 　　　　　　"1522858200",
                 　　　　　　"1522859220",
                 　　　　　　"欢乐中国人Ⅱ"
                　　　　],
                　　　　[
                　　　　　　"1522859220",
                　　　　　　"1522859700",
                　　　　　　"魅力中国城"
                　　　　],

                    */

            }
            catch (Exception e)
            {
                Debug.WriteLine("xxxxx{0}", e);
            }
            finally
            {
                //mainpage_progress_ring.IsActive = false;
            }
            
        }

        private void Select_date_combobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
