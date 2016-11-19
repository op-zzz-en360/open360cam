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

namespace open360cam
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MediaCapture mediaCapture;
        private MediaCapture mediaCapture2;
        private StorageFile photoFile;
        private StorageFile photoFile2;
        private bool isPreviewing;
        private bool isRecording;

        #region HELPER_FUNCTIONS

        enum Action
        {
            ENABLE,
            DISABLE
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

        public async 
        Task
        GetVideoProfileSupportedDeviceIdAsync()
        {

            // Finds all video capture devices
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            
            mediaCapture = new MediaCapture();
            var mediaInitSettings = new MediaCaptureInitializationSettings { VideoDeviceId = devices[0].Id };
            await mediaCapture.InitializeAsync(mediaInitSettings);

            var selectedPreviewResolution = mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).ElementAt(4);
            
            await mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, selectedPreviewResolution);
            
            // Set callbacks for failure and recording limit exceeded
            status.Text = "Device successfully initialized for video recording!";
            mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
            mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

            // Start Preview                
            previewElement.Source = mediaCapture;
            await mediaCapture.StartPreviewAsync();
     
            ///////////////////////////////////////////////////////////////////////////////////

            mediaCapture2 = new MediaCapture();
            var mediaInitSettings2 = new MediaCaptureInitializationSettings { VideoDeviceId = devices[1].Id };
            await mediaCapture2.InitializeAsync(mediaInitSettings2);


            var selectedPreviewResolution2 = mediaCapture2.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).ElementAt(4);
            
            await mediaCapture2.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, selectedPreviewResolution2);

            // Set callbacks for failure and recording limit exceeded
            status.Text = "Device successfully initialized for video recording!";
            mediaCapture2.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
            mediaCapture2.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

            // Start Preview                
            previewElement2.Source = mediaCapture2;
            await mediaCapture2.StartPreviewAsync();
            isPreviewing = true;
        }

        #endregion
        public MainPage()
        {
            this.InitializeComponent();

            SetInitButtonVisibility(Action.ENABLE);
            SetVideoButtonVisibility(Action.DISABLE);

            isRecording = false;
            isPreviewing = false;
        }        

        private async void Cleanup()
        {
            if (mediaCapture != null && mediaCapture2 != null)
            {
                // Cleanup MediaCapture object
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                    captureImage.Source = null;
                    await mediaCapture2.StopPreviewAsync();
                    captureImage2.Source = null;
                    isPreviewing = false;
                }
                if (isRecording)
                {
                    await mediaCapture.StopRecordAsync();
                    await mediaCapture2.StopRecordAsync();
                    isRecording = false;
                }                
                mediaCapture.Dispose();
                mediaCapture = null;
                mediaCapture2.Dispose();
                mediaCapture2 = null;
            }
            
            SetInitButtonVisibility(Action.ENABLE);
        }

        /// <summary>
        /// 'Initialize Audio and Video' button action function
        /// Dispose existing MediaCapture object and set it up for audio and video
        /// Enable or disable appropriate buttons
        /// - DISABLE 'Initialize Audio and Video' 
        /// - DISABLE 'Start Audio Record'
        /// - ENABLE 'Initialize Audio Only'
        /// - ENABLE 'Start Video Record'
        /// - ENABLE 'Take Photo'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void initVideo_Click(object sender, RoutedEventArgs e)
        {
            // Disable all buttons until initialization completes

            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            
            try
            {
                Cleanup();

                status.Text = "Initializing camera to capture audio and video...";
                
                await GetVideoProfileSupportedDeviceIdAsync();
                status.Text = "Camera preview succeeded";

                // Enable buttons for video and photo capture
                SetVideoButtonVisibility(Action.ENABLE);
            }
            catch (Exception ex)
            {
                status.Text = "Unable to initialize camera for audio/video mode: " + ex.Message;             
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
            
            sb.Append(currentDate.Millisecond.ToString());
            
            return sb.ToString();

        }

        /// <summary>
        /// 'Take Photo' button click action function
        /// Capture image to a file in the default account photos folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void takePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                takePhoto.IsEnabled = false;
                captureImage.Source = null;
                captureImage2.Source = null;


                string PHOTO_FILE_NAME;
                string PHOTO_FILE_NAME2;
                PHOTO_FILE_NAME = this.buildDateTimeStamp() + ".jpg";

                photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    PHOTO_FILE_NAME, CreationCollisionOption.ReplaceExisting);

                PHOTO_FILE_NAME2 = this.buildDateTimeStamp() + ".jpg";
                photoFile2 = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    PHOTO_FILE_NAME2, CreationCollisionOption.ReplaceExisting);

                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);
                await mediaCapture2.CapturePhotoToStorageFileAsync(imageProperties, photoFile2);
                takePhoto.IsEnabled = true;
                status.Text = "Take Photo succeeded: " + photoFile.Path;
                status.Text = "Take Photo succeeded: " + photoFile2.Path;

                IRandomAccessStream photoStream = await photoFile.OpenReadAsync();
                BitmapImage bitmap = new BitmapImage();
                bitmap.SetSource(photoStream);
                captureImage.Source = bitmap;
               
                IRandomAccessStream photoStream2 = await photoFile2.OpenReadAsync();
                BitmapImage bitmap2 = new BitmapImage();
                bitmap2.SetSource(photoStream2);
                captureImage2.Source = bitmap2;
                
                await App.container.CreateIfNotExistsAsync();

                CloudBlockBlob blockBlob = App.container.GetBlockBlobReference(PHOTO_FILE_NAME);
                await blockBlob.DeleteIfExistsAsync();
                await blockBlob.UploadFromFileAsync(photoFile);

                CloudBlockBlob blockBlob2 = App.container.GetBlockBlobReference(PHOTO_FILE_NAME2);
                await blockBlob2.DeleteIfExistsAsync();
                await blockBlob2.UploadFromFileAsync(photoFile2);
            }
            catch (Exception ex)
            {
                status.Text = ex.ToString();
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
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    status.Text = "MediaCaptureFailed: " + currentFailure.Message;

                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        await mediaCapture2.StopRecordAsync();
                        status.Text += "\n Recording Stopped";
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    SetInitButtonVisibility(Action.DISABLE);
                    SetVideoButtonVisibility(Action.DISABLE);
                    status.Text += "\nCheck if camera is diconnected. Try re-launching the app";                    
                }
            });            
        }

        /// <summary>
        /// Callback function if Recording Limit Exceeded
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        public async void mediaCapture_RecordLimitExceeded(Windows.Media.Capture.MediaCapture currentCaptureObject)
        {
            try
            {
                if (isRecording)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            status.Text = "Stopping Record on exceeding max record duration";
                            await mediaCapture.StopRecordAsync();
                            await mediaCapture2.StopRecordAsync();
                            isRecording = false;
                        }
                        catch (Exception e)
                        {
                            status.Text = e.Message;
                        }
                    });
                }
            }
            catch (Exception e)
            {
                status.Text = e.Message;
            }
        }
    }
}
