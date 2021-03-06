﻿// The MIT License (MIT)
//
// Copyright (c) 2013 Jacob Dufault
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Forge.Entities.Implementation.Runtime;
using Forge.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Forge.Entities {
    /// <summary>
    /// Manages the execution of a game. Allows for players to input commands to the game, thereby
    /// modifying how the game plays out.
    /// </summary>
    /// <remarks>
    /// Instances of this class are allocated when loading levels.
    /// </remarks>
    public interface IGameEngine : IDisposable {
        /// <summary>
        /// Runs a game update tick using the given input. This means that systems get executed
        /// based on the current game state and the given input.
        /// </summary>
        /// <returns>A task that is completed when the engine has finished the update.</returns>
        Task Update(IEnumerable<IGameInput> input);

        /// <summary>
        /// Synchronizes the state of game. No systems are executed in this method.
        /// </summary>
        /// <remarks>
        /// The game manager will typically run in a multithreaded context, with the rendering
        /// thread pulling data from the game. If the shared state between the game and the renderer
        /// suddenly changes half-way through a render, then tearing and generally bad things will
        /// happen. Because of this, when updating the game, no shared state between the renderer
        /// and the engine is modified. Instead, it will be modified after this method has been
        /// called.
        /// </remarks>
        /// <returns>A task that is completed when all state has been synchronized.</returns>
        Task SynchronizeState();

        /// <summary>
        /// Dispatches all of the events that have accumulated from updates since the last call to
        /// DispatchEvents(). This method operates on the calling thread, so ensure that the event
        /// handlers don't consume lots of processing time.
        /// </summary>
        void DispatchEvents();

        /// <summary>
        /// Event notifier used to notify listeners of events that have occurred inside of the
        /// engine, such as an entity being added.
        /// </summary>
        IEventNotifier EventNotifier {
            get;
        }

        /// <summary>
        /// Iterates through all data inside of the engine and returns a a snapshot of the game that
        /// reflects everything contained within the engine.
        /// </summary>
        /// <remarks>
        /// Be wary of calling this method too often; it requires that no update is occurring (it
        /// will block until the update is done) and can take a decent amount of time to calculate.
        /// It additionally performs a large number of allocations.
        /// </remarks>
        /// <returns>A content database that contains all content within the engine. All data stored
        /// inside of the database is independent of the data stored inside of the engine, so
        /// changes to the engine will not be reflected inside of the database.</returns>
        IGameSnapshot TakeSnapshot();

        /// <summary>
        /// Returns a hash code of all data inside of the engine. The hash code is computed via
        /// reflection and can be used to attempt to determine if two game engines are out of sync.
        /// </summary>
        int GetVerificationHash();
    }

    /// <summary>
    /// Allocates IGameEngines that can be used to play the state stored inside of a game snapshot.
    /// </summary>
    public static class GameEngineFactory {
        /// <summary>
        /// Creates a new game engine that can be used to simulate the game using the content from
        /// the given content database. The passed in snapshot will not be modified.
        /// </summary>
        /// <param name="snapshotJson">The serialized IGameSnapshot to use to create the
        /// engine.</param>
        /// <param name="templateJson">The serialized ITemplateGroup used to create the
        /// engine.</param>
        /// <returns>A game engine that can play the given content.</returns>
        public static Maybe<IGameEngine> CreateEngine(string snapshotJson, string templateJson) {
            try {
                IGameEngine engine = new GameEngine(snapshotJson, templateJson);
                return Maybe.Just(engine);
            }
            catch (Exception e) {
                Log.Get(typeof(GameEngineFactory)).Warn(string.Format("Failed to create engine " +
                    "with snapshot={0}, template={1}, exception={2}", snapshotJson, templateJson, e));
            }

            return Maybe<IGameEngine>.Empty;
        }

        /// <summary>
        /// Creates a new game engine that can be used to simulate the game using the content from
        /// the given content database. The passed in snapshot will not be modified.
        /// </summary>
        /// <remarks>
        /// This is a helper method; it serializes the arguments and then calls CreateEngine(string,
        /// string) which does the actual work.
        /// </remarks>
        /// <param name="snapshot">The IGameSnapshot to use to create the engine.</param>
        /// <param name="templates">The ITemplateGroup used to create the engine.</param>
        /// <returns>A game engine that can play the given content.</returns>
        public static Maybe<IGameEngine> CreateEngine(IGameSnapshot snapshot, ITemplateGroup templates) {
            return CreateEngine(LevelManager.SaveSnapshot(snapshot),
                LevelManager.SaveTemplateGroup(templates));
        }
    }
}