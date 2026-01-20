using System;
using System.Collections.Generic;
using System.Linq;

namespace ReverseEngineering.Core.ProjectSystem
{
    /// <summary>
    /// Command for undo/redo system.
    /// </summary>
    public abstract class Command
    {
        public string Description { get; set; } = "";

        public abstract void Execute();
        public abstract void Undo();

        public override string ToString() => Description;
    }

    /// <summary>
    /// Manages undo/redo history.
    /// </summary>
    public class UndoRedoManager
    {
        private readonly Stack<Command> _undoStack = [];
        private readonly Stack<Command> _redoStack = [];
        private readonly int _maxHistorySize;

        public event Action<Command>? CommandExecuted;
        public event Action? HistoryChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public UndoRedoManager(int maxHistorySize = 100)
        {
            _maxHistorySize = maxHistorySize;
        }

        // ---------------------------------------------------------
        //  OPERATIONS
        // ---------------------------------------------------------
        public void Execute(Command command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();

            // Trim history if exceeded
            if (_undoStack.Count > _maxHistorySize)
            {
                var list = _undoStack.ToList();
                _undoStack.Clear();

                for (int i = 0; i < list.Count - 1; i++)
                    _undoStack.Push(list[i]);
            }

            CommandExecuted?.Invoke(command);
            HistoryChanged?.Invoke();
        }

        public void Undo()
        {
            if (!CanUndo)
                return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            HistoryChanged?.Invoke();
        }

        public void Redo()
        {
            if (!CanRedo)
                return;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);

            HistoryChanged?.Invoke();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            HistoryChanged?.Invoke();
        }

        // ---------------------------------------------------------
        //  HISTORY QUERIES
        // ---------------------------------------------------------
        public string? GetNextUndoDescription()
        {
            return CanUndo ? _undoStack.Peek().Description : null;
        }

        public string? GetNextRedoDescription()
        {
            return CanRedo ? _redoStack.Peek().Description : null;
        }

        public IEnumerable<string> GetUndoHistory()
        {
            return _undoStack.Select(c => c.Description);
        }
    }

    /// <summary>
    /// Patch command for undo/redo.
    /// </summary>
    public class PatchCommand : Command
    {
        private readonly HexBuffer _buffer;
        private readonly int _offset;
        private readonly byte[] _originalBytes;
        private readonly byte[] _newBytes;

        public PatchCommand(HexBuffer buffer, int offset, byte[] originalBytes, byte[] newBytes, string description)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _offset = offset;
            _originalBytes = (byte[])originalBytes.Clone();
            _newBytes = (byte[])newBytes.Clone();
            Description = description;
        }

        public override void Execute()
        {
            _buffer.WriteBytes(_offset, _newBytes);
        }

        public override void Undo()
        {
            _buffer.WriteBytes(_offset, _originalBytes);
        }
    }
}
