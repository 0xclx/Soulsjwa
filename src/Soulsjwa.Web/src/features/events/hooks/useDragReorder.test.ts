import { act, renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { useDragReorder } from './useDragReorder'

const items = ['a', 'b', 'c']

const makeDragEvent = (overrides: Partial<React.DragEvent> = {}): React.DragEvent =>
  ({
    preventDefault: vi.fn(),
    dataTransfer: { effectAllowed: '', dropEffect: '', setDragImage: vi.fn() },
    clientX: 0,
    clientY: 0,
    currentTarget: { getBoundingClientRect: () => ({ top: 0, left: 0, height: 40, width: 100 }) },
    ...overrides,
  }) as unknown as React.DragEvent

describe('useDragReorder', () => {
  it('dropping on the bottom half of a later row moves the dragged item to just after it', () => {
    const onReorder = vi.fn()
    const { result } = renderHook(() => useDragReorder(items, onReorder))

    act(() => result.current.getHandleProps(0).onDragStart(makeDragEvent()))
    act(() =>
      result.current
        .getRowProps(2)
        .onDragOver(makeDragEvent({ clientY: 30 } /* bottom half of a 40px row */)),
    )
    act(() => result.current.getRowProps(2).onDrop(makeDragEvent()))

    expect(onReorder).toHaveBeenCalledWith(['b', 'c', 'a'])
  })

  it('dropping on the top half of a later row inserts before it (off-by-one shift)', () => {
    const onReorder = vi.fn()
    const { result } = renderHook(() => useDragReorder(items, onReorder))

    act(() => result.current.getHandleProps(0).onDragStart(makeDragEvent()))
    act(() =>
      result.current.getRowProps(2).onDragOver(makeDragEvent({ clientY: 10 } /* top half */)),
    )
    act(() => result.current.getRowProps(2).onDrop(makeDragEvent()))

    expect(onReorder).toHaveBeenCalledWith(['b', 'a', 'c'])
  })

  it('indicatorFor only returns a position for the currently-hovered row', () => {
    const { result } = renderHook(() => useDragReorder(items, vi.fn()))

    act(() => result.current.getHandleProps(0).onDragStart(makeDragEvent()))
    act(() =>
      result.current.getRowProps(2).onDragOver(makeDragEvent({ clientY: 10 } /* top half */)),
    )

    expect(result.current.indicatorFor(2)).toBe('before')
    expect(result.current.indicatorFor(0)).toBeNull()
    expect(result.current.indicatorFor(1)).toBeNull()
  })

  it('dropping back on its own origin is a no-op', () => {
    const onReorder = vi.fn()
    const { result } = renderHook(() => useDragReorder(items, onReorder))

    act(() => result.current.getHandleProps(1).onDragStart(makeDragEvent()))
    act(() =>
      result.current.getRowProps(1).onDragOver(makeDragEvent({ clientY: 10 } /* top half */)),
    )
    act(() => result.current.getRowProps(1).onDrop(makeDragEvent()))

    expect(onReorder).not.toHaveBeenCalled()
  })

  it('returns a stable ref callback per row index across re-renders', () => {
    const { result } = renderHook(() => useDragReorder(items, vi.fn()))

    const firstRef = result.current.getRowProps(0).ref
    // A dragover recomputes getRowProps (it depends on dragIndex/overPosition).
    act(() => result.current.getHandleProps(0).onDragStart(makeDragEvent()))
    act(() => result.current.getRowProps(2).onDragOver(makeDragEvent({ clientY: 30 })))

    expect(result.current.getRowProps(0).ref).toBe(firstRef)
  })

  it('resets drag state on drop', () => {
    const { result } = renderHook(() => useDragReorder(items, vi.fn()))

    act(() => result.current.getHandleProps(0).onDragStart(makeDragEvent()))
    expect(result.current.isDragging).toBe(true)

    act(() => result.current.getRowProps(2).onDragOver(makeDragEvent({ clientY: 30 })))
    act(() => result.current.getRowProps(2).onDrop(makeDragEvent()))

    expect(result.current.isDragging).toBe(false)
    expect(result.current.indicatorFor(2)).toBeNull()
  })
})
