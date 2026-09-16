import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Divider from '@mui/material/Divider'
import FormControl from '@mui/material/FormControl'
import InputLabel from '@mui/material/InputLabel'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Snackbar from '@mui/material/Snackbar'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { ErrorMessage, LoadingState } from '../../../../components/ui'
import { ImageUploadField } from '../../../media/components/ImageUploadField'
import { getErrorDetail } from '../../../../lib/getErrorDetail'
import { useSiteTheme } from '../hooks/useSiteTheme'
import { useUpdateSiteTheme } from '../hooks/useUpdateSiteTheme'
import { PaletteSlotField } from './PaletteSlotField'
import {
  BACKGROUND_TREATMENTS,
  SITE_FONTS,
  type SiteTheme,
  type UpdateSiteThemeRequest,
  type UploadMediaResponse,
} from '../../../../types'

const toRequest = (theme: SiteTheme): UpdateSiteThemeRequest => ({
  backgroundAssetId: theme.backgroundAssetId,
  backgroundTreatment: theme.backgroundTreatment,
  font: theme.font,
  lightDefault: theme.lightDefault,
  lightAccent: theme.lightAccent,
  lightDanger: theme.lightDanger,
  lightInfo: theme.lightInfo,
  lightSuccess: theme.lightSuccess,
  lightHighlight: theme.lightHighlight,
  darkDefault: theme.darkDefault,
  darkAccent: theme.darkAccent,
  darkDanger: theme.darkDanger,
  darkInfo: theme.darkInfo,
  darkSuccess: theme.darkSuccess,
  darkHighlight: theme.darkHighlight,
})

// ImageUploadField renders a fresh upload's response shape; a previously
// saved background only carries an id + url, so this stub fills in the
// fields the field never actually reads (width/height/byteSize/contentType)
// just to satisfy the shared type.
const toUploadStub = (assetId: string, url: string): UploadMediaResponse => ({
  assetId,
  url,
  width: 0,
  height: 0,
  byteSize: 0,
  contentType: '',
})

/** Admin editor for the site-wide theme: background, font, and the six palette slots per mode. */
export const SiteThemeEditor = () => {
  const { data, isLoading, isError } = useSiteTheme()
  const updateTheme = useUpdateSiteTheme()
  // null until the user edits — mirrors LegalDocumentEditor's draft pattern.
  const [draft, setDraft] = useState<UpdateSiteThemeRequest | null>(null)
  const [backgroundPreview, setBackgroundPreview] = useState<
    UploadMediaResponse | null | undefined
  >(undefined)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  if (isLoading) return <LoadingState label="Loading site theme…" />
  if (isError || !data) return <ErrorMessage message="Failed to load the site theme." />

  const form = draft ?? toRequest(data)
  const background =
    backgroundPreview !== undefined
      ? backgroundPreview
      : data.backgroundAssetId && data.backgroundUrl
        ? toUploadStub(data.backgroundAssetId, data.backgroundUrl)
        : null

  const update = (patch: Partial<UpdateSiteThemeRequest>) => setDraft({ ...form, ...patch })

  const handleSave = () => {
    setSaveError(null)
    updateTheme.mutate(form, {
      onSuccess: () => setSuccessMessage('Site theme saved.'),
      onError: (err) => setSaveError(getErrorDetail(err, 'Failed to save the site theme.')),
    })
  }

  return (
    <Stack spacing={3}>
      <ImageUploadField
        label="Background image"
        value={background}
        onChange={(uploaded) => {
          setBackgroundPreview(uploaded)
          update({ backgroundAssetId: uploaded?.assetId ?? null })
        }}
        disabled={updateTheme.isPending}
      />

      <Stack direction="row" sx={{ gap: 2, flexWrap: 'wrap' }}>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id="background-treatment-label">Background treatment</InputLabel>
          <Select
            labelId="background-treatment-label"
            label="Background treatment"
            value={form.backgroundTreatment}
            onChange={(e) =>
              update({
                backgroundTreatment: e.target
                  .value as UpdateSiteThemeRequest['backgroundTreatment'],
              })
            }
            disabled={updateTheme.isPending}
          >
            {BACKGROUND_TREATMENTS.map((treatment) => (
              <MenuItem key={treatment} value={treatment}>
                {treatment}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id="site-font-label">Font</InputLabel>
          <Select
            labelId="site-font-label"
            label="Font"
            value={form.font}
            onChange={(e) => update({ font: e.target.value as UpdateSiteThemeRequest['font'] })}
            disabled={updateTheme.isPending}
          >
            {SITE_FONTS.map((font) => (
              <MenuItem key={font} value={font}>
                {font}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Stack>

      <Divider />

      <Stack spacing={1}>
        <Typography variant="subtitle1">Light palette</Typography>
        <PaletteSlotField
          label="Light Default"
          value={form.lightDefault}
          onChange={(v) => update({ lightDefault: v })}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Light Accent"
          value={form.lightAccent}
          onChange={(v) => update({ lightAccent: v })}
          contrastAgainst={form.lightDefault}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Light Danger"
          value={form.lightDanger}
          onChange={(v) => update({ lightDanger: v })}
          contrastAgainst={form.lightDefault}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Light Info"
          value={form.lightInfo}
          onChange={(v) => update({ lightInfo: v })}
          contrastAgainst={form.lightDefault}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Light Success"
          value={form.lightSuccess}
          onChange={(v) => update({ lightSuccess: v })}
          contrastAgainst={form.lightDefault}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Light Highlight"
          value={form.lightHighlight}
          onChange={(v) => update({ lightHighlight: v })}
          contrastAgainst={form.lightDefault}
          disabled={updateTheme.isPending}
        />
      </Stack>

      <Stack spacing={1}>
        <Typography variant="subtitle1">Dark palette</Typography>
        <PaletteSlotField
          label="Dark Default"
          value={form.darkDefault}
          onChange={(v) => update({ darkDefault: v })}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Dark Accent"
          value={form.darkAccent}
          onChange={(v) => update({ darkAccent: v })}
          contrastAgainst={form.darkDefault}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Dark Danger"
          value={form.darkDanger}
          onChange={(v) => update({ darkDanger: v })}
          contrastAgainst={form.darkDefault}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Dark Info"
          value={form.darkInfo}
          onChange={(v) => update({ darkInfo: v })}
          contrastAgainst={form.darkDefault}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Dark Success"
          value={form.darkSuccess}
          onChange={(v) => update({ darkSuccess: v })}
          contrastAgainst={form.darkDefault}
          disabled={updateTheme.isPending}
        />
        <PaletteSlotField
          label="Dark Highlight"
          value={form.darkHighlight}
          onChange={(v) => update({ darkHighlight: v })}
          contrastAgainst={form.darkDefault}
          disabled={updateTheme.isPending}
        />
      </Stack>

      {saveError && <Alert severity="error">{saveError}</Alert>}

      <Button
        variant="contained"
        onClick={handleSave}
        disabled={updateTheme.isPending}
        sx={{ alignSelf: 'flex-start' }}
      >
        {updateTheme.isPending ? 'Saving…' : 'Save'}
      </Button>

      <Snackbar
        open={!!successMessage}
        autoHideDuration={3000}
        onClose={() => setSuccessMessage(null)}
      >
        <Alert severity="success" onClose={() => setSuccessMessage(null)}>
          {successMessage}
        </Alert>
      </Snackbar>
    </Stack>
  )
}
