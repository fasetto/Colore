// ---------------------------------------------------------------------------------------
// <copyright file="GenericDeviceImplementation.cs" company="Corale">
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

namespace Corale.Colore.Implementations
{
    using System;
    using System.Threading.Tasks;

    using Corale.Colore.Api;
    using Corale.Colore.Data;
    using Corale.Colore.Effects.Generic;
    using Corale.Colore.Logging;

    /// <inheritdoc cref="IGenericDevice" />
    /// <inheritdoc cref="DeviceImplementation" />
    /// <summary>
    /// A generic device.
    /// </summary>
    internal sealed class GenericDeviceImplementation : DeviceImplementation, IGenericDevice
    {
        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogProvider.For<GenericDeviceImplementation>();

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericDeviceImplementation" /> class.
        /// </summary>
        /// <param name="deviceId">The <see cref="Guid" /> of the device.</param>
        /// <param name="api">Reference to the Chroma API instance in use.</param>
        public GenericDeviceImplementation(Guid deviceId, IChromaApi api)
            : base(api)
        {
            Log.InfoFormat("New generic device initializing: {0}", deviceId);

            if (!Devices.IsValidId(deviceId))
                throw new UnsupportedDeviceException(deviceId);

            DeviceId = deviceId;
        }

        /// <inheritdoc />
        /// <summary>
        /// Gets the <see cref="Guid" /> of this device.
        /// </summary>
        public Guid DeviceId { get; }

        /// <inheritdoc cref="DeviceImplementation.ClearAsync" />
        /// <summary>
        /// Clears the current effect on Generic Devices.
        /// </summary>
        public override async Task<Guid> ClearAsync()
        {
            return await SetEffectAsync(
                    await Api.CreateDeviceEffectAsync(DeviceId, Effect.None, default(None)).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        /// <inheritdoc cref="DeviceImplementation.SetAllAsync" />
        /// <summary>
        /// Sets the color of all components on this device.
        /// </summary>
        /// <param name="color">Color to set.</param>
        public override Task<Guid> SetAllAsync(Color color)
        {
            throw new NotSupportedException("Setting colors is not supported on generic devices.");
        }

        /// <inheritdoc />
        /// <summary>
        /// Sets a parameter-less effect on this device.
        /// </summary>
        /// <param name="effect">Effect to set.</param>
        public async Task<Guid> SetEffectAsync(Effect effect)
        {
            return await SetEffectAsync(effect, IntPtr.Zero).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Sets an effect on this device, taking a parameter.
        /// </summary>
        /// <param name="effect">Effect to set.</param>
        /// <param name="data">Effect-specific parameter to use.</param>
        public async Task<Guid> SetEffectAsync<T>(Effect effect, T data)
            where T : struct
        {
            return await SetEffectAsync(await Api.CreateDeviceEffectAsync(DeviceId, effect, data).ConfigureAwait(false))
                .ConfigureAwait(false);
        }
    }
}
