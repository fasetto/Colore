// ---------------------------------------------------------------------------------------
// <copyright file="RestApi.cs" company="Corale">
//     Copyright © 2015-2017 by Adam Hellberg and Brandon Scott.
//
//     Permission is hereby granted, free of charge, to any person obtaining a copy of
//     this software and associated documentation files (the "Software"), to deal in
//     the Software without restriction, including without limitation the rights to
//     use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//     of the Software, and to permit persons to whom the Software is furnished to do
//     so, subject to the following conditions:
//
//     The above copyright notice and this permission notice shall be included in all
//     copies or substantial portions of the Software.
//
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
//     WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
//     CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//     "Razer" is a trademark of Razer USA Ltd.
// </copyright>
// ---------------------------------------------------------------------------------------

namespace Corale.Colore.Rest
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Common.Logging;

    using Corale.Colore.Api;
    using Corale.Colore.Data;
    using Corale.Colore.Effects.Generic;
    using Corale.Colore.Rest.Data;

    /// <inheritdoc cref="IChromaApi" />
    /// <summary>
    /// An implementation of the REST API backend for the Chroma SDK.
    /// </summary>
    internal sealed class RestApi : IChromaApi, IDisposable
    {
        /// <summary>
        /// Default endpoint for accessing the Chroma SDK on the local machine.
        /// </summary>
        internal const string DefaultEndpoint = "http://localhost:54235";

        /// <summary>
        /// Interval (in milliseconds) to wait between each heartbeat call.
        /// </summary>
        private const int HeartbeatInterval = 1000;

        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger<RestApi>();

        /// <summary>
        /// Underlying <see cref="IRestClient" /> used for API calls.
        /// </summary>
        private readonly IRestClient _client;

        /// <summary>
        /// Timer to dispatch regular heartbeat calls.
        /// </summary>
        private readonly Timer _heartbeatTimer;

        /// <summary>
        /// Keeps track of current session ID.
        /// </summary>
        private int _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestApi" /> class.
        /// </summary>
        /// <param name="client">The instance of <see cref="IRestClient" /> to use for API calls.</param>
        public RestApi(IRestClient client)
        {
            Log.InfoFormat("Initializing REST API client at {0}", client.BaseAddress);
            _client = client;
            _heartbeatTimer = new Timer(state => SendHeartbeat(), null, Timeout.Infinite, HeartbeatInterval);
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes the Chroma SDK by sending a POST request to <c>/razer/chromasdk</c>.
        /// </summary>
        /// <param name="info">Information about the application.</param>
        /// <returns>An object representing the progress of this asynchronous task.</returns>
        /// <exception cref="RestException">Thrown if there is an error calling the REST API.</exception>
        public async Task InitializeAsync(AppInfo info)
        {
            Log.Info("Initializing SDK via /razer/chromasdk endpoint");

            var response = await _client.PostAsync<RestInitResponse>("/razer/chromasdk", info).ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                var ex = new RestException(
                    "Failed to initialize Chroma REST API",
                    Result.RzFailed,
                    new Uri(_client.BaseAddress, "/razer/chromasdk"),
                    response.Status);
                Log.Error("Chroma SDK initialization failed", ex);
                throw ex;
            }

            var data = response.Data;

            if (data == null)
            {
                var ex = new RestException(
                    "REST API returned NULL data",
                    Result.RzFailed,
                    new Uri(_client.BaseAddress, "/razer/chromasdk"),
                    response.Status);
                Log.Error("Got NULL data from REST API", ex);
                throw ex;
            }

            _session = data.Session;
            _client.BaseAddress = data.Uri;

            Log.InfoFormat("New REST API session {0} at {1}", _session, _client.BaseAddress);
            Log.Debug("Starting heartbeat timer");
            _heartbeatTimer.Change(HeartbeatInterval, HeartbeatInterval);
        }

        /// <inheritdoc />
        /// <summary>
        /// Uninitializes the Chroma SDK by sending a DELETE request to <c>/</c>.
        /// </summary>
        /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
        /// <exception cref="RestException">Thrown if there is an error calling the REST API.</exception>
        /// <exception cref="ApiException">Thrown if the SDK responds with an error code.</exception>
        public async Task UninitializeAsync()
        {
            var response = await _client.DeleteAsync<RestCallResponse>("/").ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                Log.Error("Chroma SDK uninitialization failed");
                throw new RestException(
                    "Failed to uninitialize Chroma REST API",
                    response.Data?.Result ?? Result.RzFailed,
                    new Uri(_client.BaseAddress, "/"),
                    response.Status,
                    response.Data);
            }

            var data = response.Data;

            if (data == null)
                throw new ApiException("Uninitialize API returned NULL response");

            if (!data.Result)
                throw new ApiException("Exception when calling uninitialize API", data.Result);
        }

        /// <inheritdoc />
        public Task<DeviceInfo> QueryDeviceAsync(Guid deviceId)
        {
            throw new NotSupportedException("Chroma REST API does not support device querying");
        }

        /// <inheritdoc />
        /// <summary>
        /// Set effect by sending a PUT request to <c>/effect</c>.
        /// </summary>
        /// <param name="effectId">Effect ID to set.</param>
        /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
        /// <exception cref="RestException">Thrown if there is an error calling the REST API.</exception>
        /// <exception cref="ApiException">Thrown if the SDK responds with an error code.</exception>
        public async Task SetEffectAsync(Guid effectId)
        {
            var response = await _client.PutAsync<RestCallResponse>("/effect", new { id = effectId })
                                        .ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                Log.Error("Failed to set effect ID");
                throw new RestException(
                    "Failed to set effect ID",
                    response.Data?.Result ?? Result.RzFailed,
                    new Uri(_client.BaseAddress, "/effect"),
                    response.Status,
                    response.Data);
            }

            var data = response.Data;

            if (data == null)
                throw new ApiException("SetEffect API returned NULL response");

            if (!data.Result)
                throw new ApiException("Exception when calling SetEffect API", data.Result);
        }

        /// <inheritdoc />
        /// <summary>
        /// Deletes an effect with the specified <see cref="Guid" /> by sending a DELETE request to <c>/effect</c>.
        /// </summary>
        /// <param name="effectId">Effect ID to delete.</param>
        /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
        /// <exception cref="RestException">Thrown if there is an error calling the REST API.</exception>
        /// <exception cref="ApiException">Thrown if the SDK responds with an error code.</exception>
        public async Task DeleteEffectAsync(Guid effectId)
        {
            var response = await _client.DeleteAsync<RestCallResponse>("/effect", new { id = effectId })
                                        .ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                Log.Error("Failed to delete effect ID");
                throw new RestException(
                    "Failed to delete effect ID",
                    response.Data?.Result ?? Result.RzFailed,
                    new Uri(_client.BaseAddress, "/effect"),
                    response.Status,
                    response.Data);
            }

            var data = response.Data;

            if (data == null)
                throw new ApiException("DeleteEffect API returned NULL response");

            if (!data.Result)
                throw new ApiException("Exception when calling DeleteEffect API", data.Result);
        }

        /// <inheritdoc />
        public Task<Guid> CreateDeviceEffectAsync(Guid deviceId, Effect effect)
        {
            throw new NotSupportedException("Chroma REST API does not support generic device effects");
        }

        /// <inheritdoc />
        public Task<Guid> CreateDeviceEffectAsync<T>(Guid deviceId, Effect effect, T data) where T : struct
        {
            throw new NotSupportedException("Chroma REST API does not support generic device effects");
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new keyboard effect without any effect data by sending a POST request to the keyboard API.
        /// </summary>
        /// <param name="effect">The type of effect to create.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateKeyboardEffectAsync(Effects.Keyboard.Effect effect)
        {
            return await CreateEffectAsync("/keyboard", new EffectData(effect)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new keyboard effect with the specified effect data by sending a POST request to the keyboard API.
        /// </summary>
        /// <typeparam name="T">The structure type, needs to be compatible with the effect type.</typeparam>
        /// <param name="effect">The type of effect to create.</param>
        /// <param name="data">The effect structure parameter.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateKeyboardEffectAsync<T>(Effects.Keyboard.Effect effect, T data) where T : struct
        {
            return await CreateEffectAsync("/keyboard", data).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new mouse effect without any effect data by sending a POST request to the mouse API.
        /// </summary>
        /// <param name="effect">The type of effect to create.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateMouseEffectAsync(Effects.Mouse.Effect effect)
        {
            return await CreateEffectAsync("/mouse", new EffectData(effect)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new mouse effect with the specified effect data by sending a POST request to the mouse API.
        /// </summary>
        /// <typeparam name="T">The effect struct type.</typeparam>
        /// <param name="effect">The type of effect to create.</param>
        /// <param name="data">Effect options struct.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateMouseEffectAsync<T>(Effects.Mouse.Effect effect, T data) where T : struct
        {
            return await CreateEffectAsync("/mouse", data).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new headset effect without any effect data by sending a POST request to the headset API.
        /// </summary>
        /// <param name="effect">The type of effect to create.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateHeadsetEffectAsync(Effects.Headset.Effect effect)
        {
            return await CreateEffectAsync("/headset", new EffectData(effect)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new headset effect with the specified effect data by sending a POST request to the headset API.
        /// </summary>
        /// <typeparam name="T">The effect struct type.</typeparam>
        /// <param name="effect">The type of effect to create.</param>
        /// <param name="data">Effect options struct.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateHeadsetEffectAsync<T>(Effects.Headset.Effect effect, T data) where T : struct
        {
            return await CreateEffectAsync("/headset", data).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new mousepad effect without any effect data by sending a POST request to the mousepad API.
        /// </summary>
        /// <param name="effect">The type of effect to create.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateMousepadEffectAsync(Effects.Mousepad.Effect effect)
        {
            return await CreateEffectAsync("/mousepad", new EffectData(effect)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new mousepad effect with the specified effect data by sending a POST request to the mousepad API.
        /// </summary>
        /// <typeparam name="T">The effect struct type.</typeparam>
        /// <param name="effect">The type of effect to create.</param>
        /// <param name="data">Effect options struct.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateMousepadEffectAsync<T>(Effects.Mousepad.Effect effect, T data) where T : struct
        {
            return await CreateEffectAsync("/mousepad", data).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new keypad effect without any effect data by sending a POST request to the keypad API.
        /// </summary>
        /// <param name="effect">THe type of effect to create.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateKeypadEffectAsync(Effects.Keypad.Effect effect)
        {
            return await CreateEffectAsync("/keypad", new EffectData(effect)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new keypad effect with the specified effect data by sending a POST request to the keypad API.
        /// </summary>
        /// <typeparam name="T">The effect struct type.</typeparam>
        /// <param name="effect">The type of effect to create.</param>
        /// <param name="data">Effect options struct.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateKeypadEffectAsync<T>(Effects.Keypad.Effect effect, T data) where T : struct
        {
            return await CreateEffectAsync("/keypad", data).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new Chroma Link effect without any effect data by sending a POST request to the Chroma Link API.
        /// </summary>
        /// <param name="effect">The type of effect to create.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateChromaLinkEffectAsync(Effects.ChromaLink.Effect effect)
        {
            return await CreateEffectAsync("/chromalink", new EffectData(effect)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new Chroma Link effect with the specified effect data by sending a POST request to the Chroma Link API.
        /// </summary>
        /// <typeparam name="T">The effect struct type.</typeparam>
        /// <param name="effect">The type of effect to create.</param>
        /// <param name="data">Effect options struct.</param>
        /// <returns>A <see cref="Guid" /> for the created effect.</returns>
        public async Task<Guid> CreateChromaLinkEffectAsync<T>(Effects.ChromaLink.Effect effect, T data) where T : struct
        {
            return await CreateEffectAsync("/chromalink", data).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void RegisterEventNotifications(IntPtr windowHandle)
        {
            throw new NotSupportedException("Event notifications are not supported in Chroma REST API");
        }

        /// <inheritdoc />
        public void UnregisterEventNotifications()
        {
            throw new NotSupportedException("Event notifications are not supported in Chroma REST API");
        }

        /// <inheritdoc />
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _client?.Dispose();
        }

        /// <summary>
        /// Handles sending regular calls to the heartbeat API, in order to keep the connection alive.
        /// </summary>
        /// <exception cref="RestException">Thrown if there is an error calling the REST API.</exception>
        private void SendHeartbeat()
        {
#if DEBUG
            Log.Trace("Sending heartbeat");
#endif

            var response = _client.PutAsync<HeartbeatResponse>("/heartbeat").Result;

            if (!response.IsSuccessful)
            {
                var ex = new RestException(
                    "Call to heartbeat API failed",
                    Result.RzFailed,
                    new Uri(_client.BaseAddress, "/heartbeat"),
                    response.Status);
                Log.Error("Heartbeat call failed", ex);
                throw ex;
            }

            if (response.Data == null)
            {
                var ex = new RestException(
                    "Got NULL data from heartbeat call",
                    Result.RzFailed,
                    new Uri(_client.BaseAddress, "/heartbeat"),
                    response.Status);
                Log.Error("Got NULL data from heartbeat call", ex);
                throw ex;
            }

#if DEBUG
            Log.TraceFormat("Heartbeat complete, tick: {0}", response.Data.Tick);
#endif
        }

        /// <summary>
        /// Creates a Chroma effect using the specified API endpoint and effect data.
        /// </summary>
        /// <param name="endpoint">Device endpoint to create effect at.</param>
        /// <param name="data">Effect data.</param>
        /// <returns>A <see cref="Guid" /> identifying the newly created effect.</returns>
        /// <exception cref="RestException">Thrown if there is an error calling the REST API.</exception>
        /// <exception cref="ApiException">Thrown if the SDK returns an exception creating the effect.</exception>
        private async Task<Guid> CreateEffectAsync(string endpoint, object data)
        {
            var response = await _client.PostAsync<RestCallResponse>(endpoint, data).ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                var ex = new RestException(
                    $"Failed to create effect at {endpoint}",
                    response.Data?.Result ?? Result.RzFailed,
                    new Uri(_client.BaseAddress, endpoint),
                    response.Status,
                    response.Data);
                Log.Error("Failed to create effect", ex);
                throw ex;
            }

            var responseData = response.Data;

            if (responseData == null)
                throw new ApiException("Effect creation API returned NULL response");

            if (!responseData.Result)
                throw new ApiException("Exception when calling SetEffect API", responseData.Result);

            if (responseData.EffectId == null)
                throw new ApiException("Got NULL GUID from creating effect", responseData.Result);

            return responseData.EffectId.Value;
        }
    }
}
