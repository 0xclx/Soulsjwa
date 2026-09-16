import { useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import Link from '@mui/material/Link'
import Popover from '@mui/material/Popover'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import HelpOutlineIcon from '@mui/icons-material/HelpOutlineOutlined'
import { useLegalDocument } from '../hooks/useLegalDocument'

const DATENSCHUTZ_PATH = '/datenschutz'

const TRIGGER_LABEL = 'What we store when you sign in with Twitch'

interface ConsentCopy {
  /** Heading for this block; also what the language is called in itself. */
  heading: string
  /** BCP-47 tag, so screen readers switch voice per block. */
  lang: string
  body: string
  detailsPrefix: string
  /** Linked to /datenschutz once that document has content. */
  detailsLinkLabel: string
  detailsSuffix: string
}

/**
 * German first: the consent is written for the DACH audience this instance
 * is aimed at, and the German wording is the one that has to be legally
 * accurate. English follows as a translation for international competitors.
 */
const CONSENT_COPY: readonly ConsentCopy[] = [
  {
    heading: 'Deutsch',
    lang: 'de',
    body:
      'Mit der Anmeldung stimmst du zu, dass wir deine Twitch-ID, deinen Twitch-Namen und die ' +
      'in deinem Twitch-Konto hinterlegte E-Mail-Adresse speichern und auf unserer Whitelist ' +
      'freischalten, damit du dich auf dieser Website einloggen kannst.',
    detailsPrefix: 'Details findest du in unserer ',
    detailsLinkLabel: 'Datenschutzerklärung',
    detailsSuffix: '.',
  },
  {
    heading: 'English',
    lang: 'en',
    body:
      'By signing in you agree that we store your Twitch ID, your Twitch name and the email ' +
      'address registered with your Twitch account, and add them to our allowlist so that you ' +
      'can sign in to this website.',
    detailsPrefix: 'You can find the details in our ',
    detailsLinkLabel: 'privacy policy',
    detailsSuffix: '.',
  },
]

/**
 * Help icon next to the "Sign in with Twitch" button: a click explains, in
 * German and English, what leaves Twitch and lands in our database when the
 * button is used.
 *
 * A Popover rather than a Tooltip because the notice carries a link into the
 * Datenschutz page — hover copy can't be reached with a pointer or keyboard.
 * The link only renders while that document has content, mirroring AppFooter:
 * the public route 404s on an empty document, so a link there would be dead.
 */
export const TwitchConsentInfo = () => {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)
  const { data: datenschutz } = useLegalDocument('Datenschutz')
  const hasDatenschutz = !!datenschutz?.content?.trim()

  return (
    <>
      <Tooltip title={TRIGGER_LABEL}>
        <IconButton
          onClick={(event) => setAnchorEl(event.currentTarget)}
          aria-label={TRIGGER_LABEL}
          aria-haspopup="dialog"
          color="inherit"
          size="small"
        >
          <HelpOutlineIcon fontSize="small" />
        </IconButton>
      </Tooltip>
      <Popover
        open={!!anchorEl}
        anchorEl={anchorEl}
        onClose={() => setAnchorEl(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{
          paper: { role: 'dialog', 'aria-label': TRIGGER_LABEL, sx: { maxWidth: 420, p: 2 } },
        }}
      >
        <Stack spacing={1.5} divider={<Divider flexItem />}>
          {CONSENT_COPY.map((copy) => (
            <Stack key={copy.lang} spacing={0.5} lang={copy.lang}>
              <Typography variant="overline" color="text.secondary">
                {copy.heading}
              </Typography>
              <Typography variant="body2">{copy.body}</Typography>
              <Typography variant="body2">
                {copy.detailsPrefix}
                {hasDatenschutz ? (
                  <Link
                    component={RouterLink}
                    to={DATENSCHUTZ_PATH}
                    onClick={() => setAnchorEl(null)}
                  >
                    {copy.detailsLinkLabel}
                  </Link>
                ) : (
                  copy.detailsLinkLabel
                )}
                {copy.detailsSuffix}
              </Typography>
            </Stack>
          ))}
        </Stack>
      </Popover>
    </>
  )
}
