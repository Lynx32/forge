﻿using Neon.Collections;
using Neon.Entities.Implementation.Shared;
using Neon.Utilities;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neon.Entities.Implementation.Content {
    [ProtoContract(SkipConstructor = true)]
    internal class ContentEntity : IEntity {
        [ProtoMember(1)]
        private SerializableContainer _serializedCurrentData;
        [ProtoMember(2)]
        private SerializableContainer _serializedPreviousData;

        [ProtoBeforeSerialization]
        private void ExportContainers() {
            _serializedCurrentData = new SerializableContainer(_currentData.Select(pair => pair.Value));
            _serializedPreviousData = new SerializableContainer(_previousData.Select(pair => pair.Value));
        }

        [ProtoAfterDeserialization]
        private void ImportContainers() {
            _eventNotifier = new EventNotifier();

            _currentData = new SparseArray<IData>();
            foreach (IData current in _serializedCurrentData.Cast<IData>()) {
                _currentData[DataAccessorFactory.GetId(current)] = current;
            }

            _previousData = new SparseArray<IData>();
            foreach (IData previous in _serializedPreviousData.Cast<IData>()) {
                _previousData[DataAccessorFactory.GetId(previous)] = previous;
            }
        }

        private SparseArray<IData> _currentData;

        private SparseArray<IData> _previousData;

        private EventNotifier _eventNotifier;

        [ProtoMember(3)]
        public bool HasModification;

        [ProtoMember(4)]
        public int UniqueId {
            get;
            private set;
        }

        public ContentEntity(int uniqueId, string prettyName) {
            _currentData = new SparseArray<IData>();
            _previousData = new SparseArray<IData>();
            _eventNotifier = new EventNotifier();

            UniqueId = uniqueId;
            PrettyName = prettyName;
        }

        public ContentEntity(IEntity entity) :
            this(entity.UniqueId, entity.PrettyName) {
            Restore(entity);
        }

        public override string ToString() {
            if (PrettyName.Length > 0) {
                return string.Format("Entity [uid={0}, name={1}]", UniqueId, PrettyName);
            }
            else {
                return string.Format("Entity [uid={0}]", UniqueId);
            }
        }

        public void Restore(IEntity entity) {
            foreach (IData data in entity.SelectCurrentData()) {
                DataAccessor accessor = new DataAccessor(data);

                HasModification = HasModification || entity.WasModified(accessor);

                IData current = entity.Current(accessor);
                IData previous = entity.Previous(accessor);

                _currentData[accessor.Id] = current;
                _previousData[accessor.Id] = previous;
            }
        }

        public void Destroy() {
            throw new InvalidOperationException("Cannot destroy a ContentEntity");
        }

        public IData AddOrModify(DataAccessor accessor) {
            throw new InvalidOperationException("Cannot AddOrModify a ContentEntity (use Add)");
        }

        public IData AddData(DataAccessor accessor) {
            if (ContainsData(accessor) == true) {
                throw new AlreadyAddedDataException(this, accessor);
            }

            Type dataType = DataAccessorFactory.GetTypeFromAccessor(accessor);
            _currentData[accessor.Id] = (IData)Activator.CreateInstance(dataType);
            _previousData[accessor.Id] = (IData)Activator.CreateInstance(dataType);

            return _currentData[accessor.Id];
        }

        public void RemoveData(DataAccessor accessor) {
            int id = accessor.Id;

            if (_currentData.Contains(id) == false || _previousData.Contains(id) == false) {
                throw new NoSuchDataException(this, accessor);
            }

            _currentData.Remove(id);
            _previousData.Remove(id);
        }

        public IData Modify(DataAccessor accessor) {
            throw new InvalidOperationException("Cannot modify a ContentEntity");
        }

        public ICollection<IData> SelectCurrentData(Predicate<IData> filter = null,
            ICollection<IData> storage = null) {
            if (storage == null) {
                storage = new List<IData>();
            }

            foreach (var pair in _currentData) {
                IData data = pair.Value;
                if (filter == null || filter(data)) {
                    storage.Add(data);
                }
            }

            return storage;
        }

        public IEventNotifier EventNotifier {
            get { return _eventNotifier; }
        }

        public IData Current(DataAccessor accessor) {
            if (ContainsData(accessor) == false) {
                throw new NoSuchDataException(this, accessor);
            }

            return _currentData[accessor.Id];
        }

        public IData Previous(DataAccessor accessor) {
            if (ContainsData(accessor) == false) {
                throw new NoSuchDataException(this, accessor);
            }

            return _previousData[accessor.Id];
        }

        public bool ContainsData(DataAccessor accessor) {
            return _currentData.Contains(accessor.Id);
        }

        public bool WasModified(DataAccessor accessor) {
            return false;
        }

        public bool WasAdded(DataAccessor accessor) {
            return false;
        }

        public bool WasRemoved(DataAccessor accessor) {
            return false;
        }

        public string PrettyName {
            get;
            set;
        }
    }
}