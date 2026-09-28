using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Managers;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxManager
    {
        private void BuildBoard()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;
            columns = new Column[3];
            if (assignmentTable != null && assignmentTable.Entries.Count > 0)
            {
                BuildFromAssignmentTable(camera);
                return;
            }

            if (layoutBoxes != null && layoutBoxes.Length >= 3)
            {
                BuildFromSceneLayout(camera);
                return;
            }

            FruitType[][] types =
            {
                new[]
                {
                    FruitType.Orange,
                    FruitType.Strawberry
                },
                new[]
                {
                    FruitType.Strawberry,
                    FruitType.Orange
                },
                new[]
                {
                    FruitType.Orange,
                    FruitType.Strawberry
                }
            };
            BuildBoxColumns(camera, types);
        }

        private void BuildFromAssignmentTable(Camera camera)
        {
            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                List<BoxAssignmentTable.Entry> entries = new();
                for (int index = 0; index < assignmentTable.Entries.Count; index++)
                {
                    BoxAssignmentTable.Entry entry = assignmentTable.Entries[index];
                    if (entry != null && entry.box != null && entry.column == columnIndex)
                        entries.Add(entry);
                }

                entries.Sort((left, right) => left.queueOrder.CompareTo(right.queueOrder));
                if (entries.Count == 0)
                {
                    columns[columnIndex] = new Column();
                    continue;
                }

                Transform pickup = assignmentTable.GetPickupPoint(columnIndex);
                Vector3 pickupPosition = pickup != null ? pickup.position : ResolvePickupPosition(columnIndex, camera);
                Column column = new()
                {
                    ActivePosition = new Vector3((1 - columnIndex) * AuthoredColumnSpacing, AuthoredFirstRowY, entries[0].box.transform.position.z),
                    PickupDistance = loop.FindClosestPathDistance(pickupPosition)
                };
                ConfigureAssignedColumn(columnIndex, entries, column);
            }
        }

        private void BuildFromSceneLayout(Camera camera)
        {
            int rowCount = layoutBoxes.Length / 3;
            for (int columnIndex = 0; columnIndex < 3; columnIndex++)
            {
                BoxView first = layoutBoxes[columnIndex];
                Vector3 pickupProbe = ResolvePickupPosition(columnIndex, camera);
                Column column = new()
                {
                    ActivePosition = first.transform.position,
                    PickupDistance = loop.FindClosestPathDistance(pickupProbe)
                };
                columns[columnIndex] = column;
                for (int row = 0; row < rowCount; row++)
                {
                    int index = row * 3 + columnIndex;
                    if (index >= layoutBoxes.Length || layoutBoxes[index] == null)
                        continue;
                    BoxView view = layoutBoxes[index];
                    column.RowPositions.Add(view.transform.position);
                    view.SetupRuntime();
                    view.SetEditorClosed(true);
                    view.Runtime.ColumnIndex = columnIndex;
                    views[view.Runtime] = view;
                    column.Queue.Enqueue(view);
                }

                PromoteNext(column, false);
            }
        }

        private Vector3 ResolvePickupPosition(int columnIndex, Camera camera)
        {
            if (columnPickupPoints != null && columnIndex < columnPickupPoints.Length && columnPickupPoints[columnIndex] != null)
                return columnPickupPoints[columnIndex].position;
            GameObject scenePoint = GameObject.Find($"P{columnIndex + 1}");
            if (scenePoint != null)
                return scenePoint.transform.position;
            Vector3 fallback = camera.ViewportToWorldPoint(new Vector3(0.27f + columnIndex * 0.23f, 0.29f, -camera.transform.position.z));
            fallback.z = 0f;
            return fallback;
        }

        private BoxView CreateBox(FruitType type, int capacity, int columnIndex, int queueIndex, Vector3 active)
        {
            BoxView prefab = Resources.Load<BoxView>(capacity == 4 ? "Box4" : "Box6");
            BoxView view;
            if (prefab != null)
                view = Instantiate(prefab, transform);
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Quad);
                fallback.transform.SetParent(transform, false);
                view = fallback.AddComponent<BoxView>();
                view.Initialize(fallback.GetComponent<Renderer>());
            }

            view.name = $"Column {columnIndex + 1} - {type} Box {capacity}";
            view.transform.position = active + Vector3.down * (0.75f + queueIndex * 0.55f);
            view.transform.localScale *= 1.15f;
            view.Configure(type, capacity, ColorFor(type));
            view.SetupRuntime();
            view.SetEditorClosed(true);
            view.Runtime.ColumnIndex = columnIndex;
            views[view.Runtime] = view;
            return view;
        }

        internal void PromoteNext(Column column, bool shiftQueue)
        {
            column.Active = column.Queue.Count > 0 ? column.Queue.Dequeue() : null;
            if (column.Active == null)
                return;
            column.Active.Runtime.Activate();
            column.Active.PlayPromote(column.ActivePosition);
            if (!shiftQueue)
                return;
            int row = 1;
            foreach (BoxView queued in column.Queue)
            {
                if (row >= column.RowPositions.Count)
                    break;
                queued.PlayShiftTo(column.RowPositions[row]);
                row++;
            }
        }

        internal void PrepareNextBox(Column column)
        {
            if (column.Prepared != null || column.Queue.Count == 0)
                return;
            column.Prepared = column.Queue.Dequeue();
            column.Prepared.Runtime.Activate();
            column.Prepared.PlayOpenForPromotion();
        }

        internal void MovePreparedBoxIntoActivePosition(Column column)
        {
            if (column.Prepared == null)
                return;
            column.Active = column.Prepared;
            column.Prepared = null;
            column.Active.PlayPromoteFromOpen(column.ActivePosition);
            ShiftQueueBehindActive(column);
        }

        internal void ShiftQueueBehindActive(Column column)
        {
            int row = 1;
            foreach (BoxView queued in column.Queue)
            {
                if (row >= column.RowPositions.Count)
                    break;
                queued.PlayShiftTo(column.RowPositions[row]);
                row++;
            }
        }

        private bool IsHighestPriorityBox(FruitType type, int targetColumnIndex)
        {
            // In the authored layout C1/index 0 is physically on the right and
            // C3/index 2 is on the left. Lower indices therefore have priority.
            for (int i = 0; i < targetColumnIndex; i++)
            {
                BoxView box = columns[i].Active;
                if (box != null && box.Runtime.FruitType == type && box.Runtime.CanReserve)
                    return false;
            }

            return true;
        }

        private int FindPriorityColumn(FruitType type)
        {
            // C1/index 0 is the rightmost physical column.
            for (int index = 0; index < columns.Length; index++)
            {
                BoxView box = columns[index].Active;
                if (box == null || box.Runtime == null || box.Runtime.FruitType != type)
                    continue;
                return box.Runtime.CanReserve ? index : -1;
            }

            return -1;
        }

        private void BuildBoxColumns(Camera camera, FruitType[][] types)
        {
            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                float viewportX = 0.27f + columnIndex * 0.23f;
                Vector3 active = camera.ViewportToWorldPoint(new Vector3(viewportX, 0.105f, -camera.transform.position.z));
                active.z = 0f;
                Vector3 pickupProbe = camera.ViewportToWorldPoint(new Vector3(viewportX, 0.29f, -camera.transform.position.z));
                pickupProbe.z = 0f;
                Column column = new()
                {
                    ActivePosition = active,
                    PickupDistance = loop.FindClosestPathDistance(pickupProbe)
                };
                columns[columnIndex] = column;
                for (int queueIndex = 0; queueIndex < 2; queueIndex++)
                {
                    int capacity = queueIndex == 0 ? 4 : 6;
                    BoxView view = CreateBox(types[columnIndex][queueIndex], capacity, columnIndex, queueIndex, active);
                    column.Queue.Enqueue(view);
                }

                PromoteNext(column, false);
            }
        }

        private void ConfigureAssignedColumn(int columnIndex, List<BoxAssignmentTable.Entry> entries, Column column)
        {
            columns[columnIndex] = column;
            for (int index = 0; index < entries.Count; index++)
            {
                BoxAssignmentTable.Entry entry = entries[index];
                BoxView view = entry.box;
                Vector3 layoutPosition = view.transform.position;
                layoutPosition.x = (1 - columnIndex) * AuthoredColumnSpacing;
                layoutPosition.y = AuthoredFirstRowY - entry.queueOrder * AuthoredRowSpacing;
                view.transform.position = layoutPosition;
                view.Configure(entry.fruitType, view.Capacity, assignmentTable.GetColor(entry.fruitType));
                view.SetupRuntime();
                view.SetEditorClosed(true);
                view.Runtime.ColumnIndex = columnIndex;
                views[view.Runtime] = view;
                column.RowPositions.Add(view.transform.position);
                column.Queue.Enqueue(view);
            }

            PromoteNext(column, false);
        }
    }
}
