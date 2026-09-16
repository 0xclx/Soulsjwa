import { useCallback, useRef, useState } from 'react'
import { reorderList } from '../dragReorder'

export type DragPosition = 'before' | 'after'

export interface DragHandleProps {
  draggable: true
  onDragStart: (e: React.DragEvent) => void
  onDragEnd: () => void
  style: { cursor: string }
}

export interface DragRowProps {
  ref: (el: HTMLElement | null) => void
  onDragOver: (e: React.DragEvent) => void
  onDrop: (e: React.DragEvent) => void
}

/**
 * Native-HTML5-drag-and-drop reordering for a flat list, shared by the games
 * list, objective-category groups, and objectives-within-a-category. Tracks
 * which row is being dragged and which gap the pointer is currently over (so
 * callers can render a drop-indicator line), and computes the final index
 * math (including the off-by-one shift when dragging downward past its own
 * origin) so every call site does the same thing the same way.
 */
export function useDragReorder<T>(items: readonly T[], onReorder: (next: T[]) => void) {
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [overIndex, setOverIndex] = useState<number | null>(null)
  const [overPosition, setOverPosition] = useState<DragPosition | null>(null)
  const rowRefs = useRef(new Map<number, HTMLElement>())
  // Cache one ref callback per index for the hook's lifetime. `getRowProps`
  // is recreated on every `dragIndex`/`overPosition` change (i.e. on every
  // `dragover`), so without this cache each row got a brand-new `ref`
  // callback on every pointer move, detaching and re-attaching its DOM ref
  // many times per second during a drag.
  const rowRefCallbacks = useRef(new Map<number, (el: HTMLElement | null) => void>())

  const setRowRef = useCallback((index: number) => {
    let callback = rowRefCallbacks.current.get(index)
    if (!callback) {
      callback = (el: HTMLElement | null) => {
        if (el) rowRefs.current.set(index, el)
        else rowRefs.current.delete(index)
      }
      rowRefCallbacks.current.set(index, callback)
    }
    return callback
  }, [])

  const reset = useCallback(() => {
    setDragIndex(null)
    setOverIndex(null)
    setOverPosition(null)
  }, [])

  const getHandleProps = useCallback(
    (index: number) => ({
      draggable: true as const,
      onDragStart: (e: React.DragEvent) => {
        setDragIndex(index)
        e.dataTransfer.effectAllowed = 'move'
        // Show the whole row as the drag image (not just the tiny grip
        // icon), positioned so it tracks the cursor at the same offset the
        // pointer had within the row when the drag started.
        const rowEl = rowRefs.current.get(index)
        if (rowEl) {
          const rect = rowEl.getBoundingClientRect()
          e.dataTransfer.setDragImage(rowEl, e.clientX - rect.left, e.clientY - rect.top)
        }
      },
      onDragEnd: reset,
      style: { cursor: 'grab' },
    }),
    [reset],
  )

  const getRowProps = useCallback(
    (index: number) => ({
      ref: setRowRef(index),
      onDragOver: (e: React.DragEvent) => {
        if (dragIndex === null) return
        // Must preventDefault unconditionally while a drag from this list is
        // in progress, or the browser shows a "not allowed" cursor over
        // parts of the row it doesn't think are a valid drop target, which
        // is what caused the cursor to flicker.
        e.preventDefault()
        e.dataTransfer.dropEffect = 'move'
        const rect = e.currentTarget.getBoundingClientRect()
        const position: DragPosition = e.clientY - rect.top < rect.height / 2 ? 'before' : 'after'
        setOverIndex(index)
        setOverPosition(position)
      },
      onDrop: (e: React.DragEvent) => {
        e.preventDefault()
        if (dragIndex === null) {
          reset()
          return
        }
        let targetIndex = overPosition === 'after' ? index + 1 : index
        if (dragIndex < targetIndex) targetIndex -= 1
        if (targetIndex !== dragIndex) {
          onReorder(reorderList(items, dragIndex, targetIndex))
        }
        reset()
      },
    }),
    [dragIndex, overPosition, items, onReorder, reset, setRowRef],
  )

  /** 'before' | 'after' | null — which edge of this row (if any) should show the blue drop-indicator line. */
  const indicatorFor = useCallback(
    (index: number): DragPosition | null => (overIndex === index ? overPosition : null),
    [overIndex, overPosition],
  )

  return {
    isDragging: dragIndex !== null,
    isDraggingIndex: (index: number) => dragIndex === index,
    indicatorFor,
    getHandleProps,
    getRowProps,
  }
}
