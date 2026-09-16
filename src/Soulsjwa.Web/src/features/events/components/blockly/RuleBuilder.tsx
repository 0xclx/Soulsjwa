import { useEffect, useRef, useCallback, useState } from 'react'
import * as Blockly from 'blockly'
import Box from '@mui/material/Box'
import TextField from '@mui/material/TextField'
import InputAdornment from '@mui/material/InputAdornment'
import SearchIcon from '@mui/icons-material/Search'
import type { GameDataPoint, PredefinedObjective } from '../../../../types'
import { registerJsonLogicBlocks } from './blocks'
import { buildToolbox } from './toolbox'
import { workspaceToJsonLogic } from './jsonLogicConverter'
import { registerPredefinedObjectivesCategory } from './predefinedObjectivesCategory'
import { loadRuleIntoWorkspace } from './jsonLogicToWorkspace'
import './blockly.css'

export interface RuleBuilderProps {
  dataPoints: GameDataPoint[]
  predefinedObjectives?: PredefinedObjective[]
  /** Includes the "other competitors completed" count block (fail-rule builder only). */
  includeCompetitorCompletions?: boolean
  /**
   * An existing JsonLogic rule to seed the canvas with (e.g. when editing an
   * objective). Only read once, on mount — changing it afterwards has no
   * effect, since the canvas is the source of truth once the user starts
   * editing. Ignored if the rule's shape isn't one the builder can produce
   * (e.g. hand-written JsonLogic); the canvas is just left empty in that case.
   */
  initialRule?: string
  onChange: (rule: string | undefined) => void
}

export const RuleBuilder = ({
  dataPoints,
  predefinedObjectives,
  includeCompetitorCompletions,
  initialRule,
  onChange,
}: RuleBuilderProps) => {
  const containerRef = useRef<HTMLDivElement>(null)
  const workspaceRef = useRef<Blockly.WorkspaceSvg | null>(null)
  const [objectiveFilter, setObjectiveFilter] = useState('')
  const objectiveFilterRef = useRef('')

  const handleWorkspaceChange = useCallback(() => {
    if (!workspaceRef.current) return
    const rule = workspaceToJsonLogic(workspaceRef.current)
    onChange(rule ? JSON.stringify(rule) : undefined)
  }, [onChange])

  useEffect(() => {
    if (!containerRef.current) return

    registerJsonLogicBlocks(dataPoints, predefinedObjectives, { includeCompetitorCompletions })

    const toolbox = buildToolbox(dataPoints, predefinedObjectives, { includeCompetitorCompletions })
    const workspace = Blockly.inject(containerRef.current, {
      toolbox,
      grid: { spacing: 20, length: 3, colour: '#ccc', snap: true },
      zoom: {
        controls: true,
        wheel: true,
        startScale: 1.0,
        maxScale: 2,
        minScale: 0.5,
        scaleSpeed: 1.2,
      },
      trashcan: true,
    })

    workspaceRef.current = workspace
    workspace.addChangeListener(handleWorkspaceChange)
    registerPredefinedObjectivesCategory(
      workspace,
      predefinedObjectives ?? [],
      () => objectiveFilterRef.current,
    )
    loadRuleIntoWorkspace(workspace, initialRule, predefinedObjectives ?? [])

    // Blockly measures its container on inject, but when it mounts inside a
    // dialog (or any element that animates/resizes into place) that measurement
    // is stale, leaving the toolbox and canvas mis-rendered. Re-flow on the next
    // frame and whenever the container's size changes so the builder is usable.
    const resize = () => Blockly.svgResize(workspace)
    const raf = requestAnimationFrame(resize)
    const observer = new ResizeObserver(resize)
    observer.observe(containerRef.current)

    return () => {
      cancelAnimationFrame(raf)
      observer.disconnect()
      workspace.removeChangeListener(handleWorkspaceChange)
      workspace.dispose()
      workspaceRef.current = null
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- initialRule is intentionally read once on mount only
  }, [dataPoints, predefinedObjectives, includeCompetitorCompletions, handleWorkspaceChange])

  // Kept out of the injection effect above so typing a filter never
  // reinjects the workspace (which would wipe out any blocks already placed).
  useEffect(() => {
    objectiveFilterRef.current = objectiveFilter
    workspaceRef.current?.getToolbox()?.refreshSelection()
  }, [objectiveFilter])

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, width: '100%' }}>
      {predefinedObjectives && predefinedObjectives.length > 0 && (
        <TextField
          size="small"
          placeholder="Filter predefined objectives…"
          value={objectiveFilter}
          onChange={(e) => setObjectiveFilter(e.target.value)}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              ),
            },
          }}
        />
      )}
      <Box
        ref={containerRef}
        role="region"
        aria-label="Objective auto-completion rule builder"
        tabIndex={0}
        sx={{
          width: '100%',
          height: 420,
          border: 1,
          borderColor: 'divider',
          overflow: 'hidden',
        }}
      />
    </Box>
  )
}
