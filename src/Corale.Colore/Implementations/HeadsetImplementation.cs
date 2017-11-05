// ---------------------------------------------------------------------------------------
// <copyright file="HeadsetImplementation.cs" company="Corale">
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

    using Common.Logging;

    using Corale.Colore.Api;
    using Corale.Colore.Effects.Headset;

    /// <inheritdoc cref="IHeadset" />
    /// <inheritdoc cref="Device" />
    /// <summary>
    /// Class for interacting with Chroma Headsets.
    /// </summary>
    public sealed class HeadsetImplementation : Device, IHeadset
    {
        /// <summary>
        /// Loggers instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(HeadsetImplementation));

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the <see cref="HeadsetImplementation" /> class.
        /// </summary>
        /// <param name="api">Reference to the Chroma API instance in use.</param>
        public HeadsetImplementation(IChromaApi api)
            : base(api)
        {
            Log.Info("Headset is initializing");
        }

        /// <inheritdoc cref="Device.SetAllAsync" />
        /// <summary>
        /// Sets the color of all components on this device.
        /// </summary>
        /// <param name="color">Color to set.</param>
        public override async Task<Guid> SetAllAsync(Color color)
        {
            return await SetStaticAsync(new Static(color)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Sets an effect on the headset that doesn't
        /// take any parameters, currently only valid
        /// for the <see cref="Effect.None" /> effect.
        /// </summary>
        /// <param name="effect">The type of effect to set.</param>
        public async Task<Guid> SetEffectAsync(Effect effect)
        {
            return await SetEffectAsync(await Api.CreateHeadsetEffectAsync(effect).ConfigureAwait(false)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Sets a new static effect on the headset.
        /// </summary>
        /// <param name="effect">
        /// An instance of the <see cref="Static" /> struct
        /// describing the effect.
        /// </param>
        public async Task<Guid> SetStaticAsync(Static effect)
        {
            return await SetEffectAsync(await Api.CreateHeadsetEffectAsync(Effect.Static, effect).ConfigureAwait(false)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// Sets a new <see cref="Static" /> effect on
        /// the headset using the specified <see cref="Color" />.
        /// </summary>
        /// <param name="color"><see cref="Color" /> of the effect.</param>
        public async Task<Guid> SetStaticAsync(Color color)
        {
            return await SetStaticAsync(new Static(color)).ConfigureAwait(false);
        }

        /// <inheritdoc cref="Device.ClearAsync" />
        /// <summary>
        /// Clears the current effect on the Headset.
        /// </summary>
        public override async Task<Guid> ClearAsync()
        {
            return await SetEffectAsync(Effect.None).ConfigureAwait(false);
        }
    }
}