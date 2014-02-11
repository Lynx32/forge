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

using Forge.Entities.Implementation.Content;
using Forge.Entities.Implementation.ContextObjects;
using Forge.Entities.Implementation.Runtime;
using Forge.Entities.Implementation.Shared;
using Forge.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Entities {
    /// <summary>
    /// Facilitates the creation, saving, and loading of snapshots and template groups. This is the
    /// API point that all serialization occurs in.
    /// </summary>
    public static class LevelManager {
        /// <summary>
        /// Returns an empty game snapshot.
        /// </summary>
        public static IGameSnapshot CreateSnapshot() {
            return new GameSnapshot();
        }

        /// <summary>
        /// Returns an empty template group with a starting TemplateId of 0.
        /// </summary>
        public static ITemplateGroup CreateTemplateGroup() {
            return new TemplateGroup();
        }

        /// <summary>
        /// Returns an empty template group.
        /// </summary>
        /// <param name="startingId">The lowest value for TemplateId in the group, which must be
        /// greater than or equal to 0.</param>
        public static ITemplateGroup CreateTemplateGroup(int startingId) {
            Contract.Requires(startingId >= 0);

            var group = new TemplateGroup();
            // We consume startingId-1 so that the first id generator is equal to startingId
            group.TemplateIdGenerator.Consume(startingId - 1);
            return group;
        }

        /// <summary>
        /// Merges a set of serialized template groups together into one template group. An
        /// InvalidOperationException is thrown if there are two templates with the same TemplateId.
        /// </summary>
        /// <param name="groups">The serialized template groups to merge.</param>
        /// <returns>A serialized template group that contains all of the templates within the given
        /// groups.</returns>
        public static string MergeTemplateGroups(IEnumerable<string> groups) {
            // deserialize all of the groups
            List<ITemplateGroup> deserializedGroups = new List<ITemplateGroup>();
            foreach (string serializedGroup in groups) {
                var group = LoadTemplateGroup(serializedGroup);
                deserializedGroups.Add(group);
            }

            // merge them
            ITemplateGroup merged = MergeTemplateGroups(deserializedGroups);

            // return the serialized merge result
            return SaveTemplateGroup(merged);
        }

        /// <summary>
        /// Merges a set of template groups together into one template group. An
        /// InvalidOperationException is thrown if there are two templates with the same TemplateId.
        /// </summary>
        /// <param name="groups">The template groups to merge.</param>
        /// <returns>A single template group that contains all of the templates within the given
        /// groups.</returns>
        /// <exception cref="InvalidOperationException">There were duplicate TemplateIds</exception>
        public static ITemplateGroup MergeTemplateGroups(IEnumerable<ITemplateGroup> groups) {
            // We store all of our templates in result.
            var result = new TemplateGroup();

            // ids verifies that every template has a unique TemplateId by checking for insertion
            // failures (hash collisions) -- we could also use a SparseArray, but that could be
            // * very* slow if the template groups go into the high ranges.
            var ids = new HashSet<int>();

            foreach (TemplateGroup templateGroup in groups.Cast<TemplateGroup>()) {
                foreach (ITemplate template in templateGroup.Templates) {
                    int id = template.TemplateId;

                    // verify that we don't have any duplicate template ids
                    if (ids.Add(id) == false) {
                        throw new InvalidOperationException("Duplicate template with id=" + id);
                    }

                    // add the template to the result and consume its id
                    result.Templates.Add(template);
                    result.TemplateIdGenerator.Consume(id);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a game snapshot to JSON that can be restored later.
        /// </summary>
        public static string SaveSnapshot(IGameSnapshot snapshot) {
            return SerializationHelpers.Serialize<GameSnapshot>((GameSnapshot)snapshot,
                RequiredConverters.GetConverters(),
                RequiredConverters.GetContextObjects(Maybe<GameEngine>.Empty));
        }

        /// <summary>
        /// Converts a template group to JSON that can be restored later.
        /// </summary>
        public static string SaveTemplateGroup(ITemplateGroup templates) {
            return SerializationHelpers.Serialize<TemplateGroup>((TemplateGroup)templates,
                RequiredConverters.GetConverters(),
                RequiredConverters.GetContextObjects(Maybe<GameEngine>.Empty));
        }

        /// <summary>
        /// Loads an IGameSnapshot from the given JSON and the given template group. The JSON should
        /// have been generated by calling SaveSnapshot.
        /// </summary>
        /// <exception cref="DeserializationException">An error loading the snapshot
        /// occurred.</exception>
        public static IGameSnapshot LoadSnapshot(string snapshotJson, string templateJson) {
            try {
                var restorer = GameSnapshotRestorer.Restore(snapshotJson, templateJson,
                    Maybe<GameEngine>.Empty);
                return restorer.GameSnapshot;
            }
            catch (Exception e) {
                throw new DeserializationException("snapshot=" + snapshotJson +
                    ", template=" + templateJson, e);
            }
        }

        /// <summary>
        /// Loads an ITemplateGroup from the given JSON. The JSON should have been generated by
        /// calling SaveTemplates.
        /// </summary>
        /// <exception cref="DeserializationException">An error loading the group
        /// occurred.</exception>
        public static ITemplateGroup LoadTemplateGroup(string json) {
            try {
                ITemplateGroup group = SerializationHelpers.Deserialize<TemplateGroup>(json,
                                RequiredConverters.GetConverters(),
                                RequiredConverters.GetContextObjects(Maybe<GameEngine>.Empty,
                                    new TemplateConversionContext()));
                return group;
            }
            catch (Exception e) {
                throw new DeserializationException(json, e);
            }
        }
    }
}