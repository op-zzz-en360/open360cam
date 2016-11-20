/*
    Copyright(c) Microsoft Open Technologies, Inc. All rights reserved.

    The MIT License(MIT)

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text;
using Microsoft.WindowsAzure.Storage.Auth;
using Windows.UI.Xaml.Media;

namespace open360cam
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private int[] indexArray = new int[] {0,1};
        private Image[] cameraImageArray = new Image[2];
        private MediaCapture[] mediaCaptureArray = new MediaCapture[2];
        private bool[] isPreviewingArray = new bool[2];

        #region HELPER_FUNCTIONS

        enum Action
        {
            ENABLE,
            DISABLE
        }

        private void WriteLog(string message)
        {
            status.Text += "\n " + message;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(status, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        /// <summary>
        /// Helper function to enable or disable Initialization buttons
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetInitButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                video_init.IsEnabled = true;
            }
            else
            {
                video_init.IsEnabled = false;
            }
        }

        /// <summary>
        /// Helper function to enable or disable video related buttons (TakePhoto, Start Video Record)
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetVideoButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                takePhoto.IsEnabled = true;
                takePhoto.Visibility = Visibility.Visible;
            }
            else
            {
                takePhoto.IsEnabled = false;
                takePhoto.Visibility = Visibility.Collapsed;
            }
        }

        public async Task InitDevices()
        {

            // Finds all video capture devices
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            mediaCaptureArray[0] = new MediaCapture();
            var mediaInitSettings = new MediaCaptureInitializationSettings { VideoDeviceId = devices[0].Id };
            await mediaCaptureArray[0].InitializeAsync(mediaInitSettings);

            var selectedPreviewResolution = mediaCaptureArray[0].VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).ElementAt(4);
            
            await mediaCaptureArray[0].VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, selectedPreviewResolution);

            // Set callbacks for failure and recording limit exceeded
            WriteLog("Device " + 0 + " successfully initialized!");
            mediaCaptureArray[0].Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
            mediaCaptureArray[0].RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

            // Start Preview                
            previewElement.Source = mediaCaptureArray[0];
            await mediaCaptureArray[0].StartPreviewAsync();
            isPreviewingArray[0] = true;
            
            ///////////////////////////////////////////////////////////////////////////////////

            mediaCaptureArray[1] = new MediaCapture();
            var mediaInitSettings2 = new MediaCaptureInitializationSettings { VideoDeviceId = devices[1].Id };
            await mediaCaptureArray[1].InitializeAsync(mediaInitSettings2);


            var selectedPreviewResolution2 = mediaCaptureArray[1].VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).ElementAt(4);
            
            await mediaCaptureArray[1].VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, selectedPreviewResolution2);

            // Set callbacks for failure and recording limit exceeded
            WriteLog("Device " + 1 + " successfully initialized!");
            mediaCaptureArray[1].Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
            mediaCaptureArray[1].RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

            // Start Preview                
            previewElement2.Source = mediaCaptureArray[1];
            await mediaCaptureArray[1].StartPreviewAsync();
            isPreviewingArray[1] = true;
        }

        #endregion
        public MainPage()
        {
            this.InitializeComponent();

            cameraImageArray[0] = captureImage;
            cameraImageArray[1] = captureImage2;
            SetInitButtonVisibility(Action.ENABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            
            isPreviewingArray[0] = false;
            isPreviewingArray[1] = false;
        }        

        private async void Cleanup()
        {
            if (mediaCaptureArray[0] != null)
            {
                if (isPreviewingArray[0])
                {
                    await mediaCaptureArray[0].StopPreviewAsync();
                    cameraImageArray[0].Source = null;
                    isPreviewingArray[0] = false;
                }
                mediaCaptureArray[0].Dispose();
                mediaCaptureArray[0] = null;
            }
            if (mediaCaptureArray[1] != null)
            {
                if (isPreviewingArray[1])
                {
                    await mediaCaptureArray[1].StopPreviewAsync();
                    cameraImageArray[1].Source = null;
                    isPreviewingArray[1] = false;
                }
                mediaCaptureArray[1].Dispose();
                mediaCaptureArray[1] = null;
            }
            
            SetInitButtonVisibility(Action.ENABLE);
        }

        /// <summary>
        /// 'Initialize Audio and Video' button action function
        /// Dispose existing MediaCapture object and set it up for audio and video
        /// Enable or disable appropriate buttons
        /// - DISABLE 'Initialize Camera'
        /// - ENABLE 'Take Photo'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void initVideo_Click(object sender, RoutedEventArgs e)
        {
            // Disable all buttons until initialization completes
            
            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);

            await App.container.CreateIfNotExistsAsync();

            try
            {
                Cleanup();
                WriteLog("Initializing camera to capture audio and video...");
                
                await InitDevices();
                WriteLog("Camera preview succeeded!");

                // Enable buttons for video and photo capture
                SetVideoButtonVisibility(Action.ENABLE);
            }
            catch (Exception ex)
            {
                WriteLog("Unable to initialize camera: " + ex.Message);             
            }
        }

        private void cleanup_Click(object sender, RoutedEventArgs e)
        {
            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            Cleanup();            
        }

        private string buildDateTimeStamp()
        {
            StringBuilder sb = new StringBuilder();
            DateTime currentDate = DateTime.Now;

            sb.Append(currentDate.Year.ToString());
            
            if (currentDate.Month.ToString().Length == 1)
            {
                sb.Append("0" + currentDate.Month.ToString());
            }
            else
            {
                sb.Append(currentDate.Month.ToString());
            }

            if (currentDate.Day.ToString().Length == 1)
            {
                sb.Append("0" + currentDate.Day.ToString());
            }
            else
            {
                sb.Append(currentDate.Day.ToString());
            }

            if (currentDate.Hour.ToString().Length == 1)
            {
                sb.Append("0" + currentDate.Hour.ToString());
            }
            else
            {
                sb.Append(currentDate.Hour.ToString());
            }

            if (currentDate.Minute.ToString().Length == 1)
            {
                sb.Append("0" + currentDate.Minute.ToString());
            }
            else
            {
                sb.Append(currentDate.Minute.ToString());
            }

            if (currentDate.Second.ToString().Length == 1)
            {
                sb.Append("0" + currentDate.Second.ToString());
            }
            else
            {
                sb.Append(currentDate.Second.ToString());
            }
            sb.Append("-");
            sb.Append(currentDate.Millisecond.ToString());
            
            return sb.ToString();

        }

        /// <summary>
        /// 'Take Photo' button click action function
        /// Capture image to a file in the default account photos folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void takePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                takePhoto.IsEnabled = false;

                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();

                Parallel.ForEach(indexArray, async (currentIndex) =>
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, async () =>
                             {
                                 await mediaCaptureArray[currentIndex].StopPreviewAsync();
                                 cameraImageArray[currentIndex].Source = null;
                                 string PHOTO_FILE_NAME = this.buildDateTimeStamp() + ".jpg";
                                 WriteLog("Device " + currentIndex + " started capturing photo");
                                 StorageFile photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                                                     PHOTO_FILE_NAME, CreationCollisionOption.ReplaceExisting);
                                 await mediaCaptureArray[currentIndex].CapturePhotoToStorageFileAsync(imageProperties, photoFile);
                                 WriteLog("Device " + currentIndex + " took photo succesfully: " + photoFile.Path);
                                 IRandomAccessStream photoStream = await photoFile.OpenReadAsync();
                                 BitmapImage bitmap = new BitmapImage();
                                 bitmap.SetSource(photoStream);
                                 cameraImageArray[currentIndex].Source = bitmap;

                                 await mediaCaptureArray[currentIndex].StartPreviewAsync();
                                 WriteLog("Device " + currentIndex + " started upload");
                                 CloudBlockBlob blockBlob = App.container.GetBlockBlobReference(PHOTO_FILE_NAME);
                                 await blockBlob.DeleteIfExistsAsync();
                                 await blockBlob.UploadFromFileAsync(photoFile);
                                 WriteLog("Device " + currentIndex + " finished upload successfully!");
                             });
                    });
                
                takePhoto.IsEnabled = true;
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
                Cleanup();
            }
            finally
            {
                takePhoto.IsEnabled = true;
            }
        }

        /// <summary>
        /// Callback function for any failures in MediaCapture operations
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        /// <param name="currentFailure"></param>
        private async void mediaCapture_Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                SetInitButtonVisibility(Action.DISABLE);
                SetVideoButtonVisibility(Action.DISABLE);
                WriteLog("Check if camera is diconnected. Try re-launching the app");
            });            
        }

        /// <summary>
        /// Callback function if Recording Limit Exceeded
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        public void mediaCapture_RecordLimitExceeded(Windows.Media.Capture.MediaCapture currentCaptureObject)
        {
            try
            {
                //TODO: handle record limit exceeded error
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
            }
        }
    }
}
