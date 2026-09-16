// Zips `dist-twitch/` into `soulsjwa-twitch-extension.zip`, the file the
// Twitch developer console takes under Files > Upload Version in Assets.
// Twitch wants the files at the zip's root (not inside a folder), which is
// what this produces. A small ZIP writer rather than a dependency: the format
// needed here (deflate entries + central directory) is a page of code, and it
// keeps the package's dependency list, and THIRD-PARTY-NOTICES, unchanged.
import { createWriteStream } from 'node:fs'
import { readdir, readFile, stat } from 'node:fs/promises'
import { join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { deflateRawSync } from 'node:zlib'

const here = fileURLToPath(new URL('.', import.meta.url))
const source = resolve(here, '..', 'dist-twitch')
const target = resolve(here, '..', 'soulsjwa-twitch-extension.zip')

const DEFLATE = 8
const VERSION_NEEDED = 20
const UTF8_FLAG = 0x0800
const LOCAL_HEADER = 0x04034b50
const CENTRAL_HEADER = 0x02014b50
const END_OF_CENTRAL = 0x06054b50

const crcTable = new Uint32Array(256).map((_, n) => {
  let c = n
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
  return c >>> 0
})
const crc32 = (bytes) => {
  let crc = 0xffffffff
  for (const b of bytes) crc = crcTable[(crc ^ b) & 0xff] ^ (crc >>> 8)
  return (crc ^ 0xffffffff) >>> 0
}

// A fixed timestamp keeps the zip byte-identical for identical inputs.
const dosTime = 0
const dosDate = ((2026 - 1980) << 9) | (1 << 5) | 1

async function* walk(dir) {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) yield* walk(path)
    else yield path
  }
}

const u16 = (n) => {
  const b = Buffer.alloc(2)
  b.writeUInt16LE(n)
  return b
}
const u32 = (n) => {
  const b = Buffer.alloc(4)
  b.writeUInt32LE(n >>> 0)
  return b
}

try {
  await stat(source)
} catch {
  console.error(`Nothing to zip: ${source} does not exist. Run the Twitch build first.`)
  process.exit(1)
}

const files = []
for await (const path of walk(source)) files.push(path)
files.sort()

const out = createWriteStream(target)
let offset = 0
const central = []
const write = (buffer) => {
  out.write(buffer)
  offset += buffer.length
}

for (const path of files) {
  const name = Buffer.from(relative(source, path).split('\\').join('/'), 'utf8')
  const data = await readFile(path)
  const compressed = deflateRawSync(data)
  const crc = crc32(data)
  const headerOffset = offset
  write(
    Buffer.concat([
      u32(LOCAL_HEADER),
      u16(VERSION_NEEDED),
      u16(UTF8_FLAG),
      u16(DEFLATE),
      u16(dosTime),
      u16(dosDate),
      u32(crc),
      u32(compressed.length),
      u32(data.length),
      u16(name.length),
      u16(0),
      name,
      compressed,
    ]),
  )
  central.push(
    Buffer.concat([
      u32(CENTRAL_HEADER),
      u16(VERSION_NEEDED),
      u16(VERSION_NEEDED),
      u16(UTF8_FLAG),
      u16(DEFLATE),
      u16(dosTime),
      u16(dosDate),
      u32(crc),
      u32(compressed.length),
      u32(data.length),
      u16(name.length),
      u16(0),
      u16(0),
      u16(0),
      u16(0),
      u32(0),
      u32(headerOffset),
      name,
    ]),
  )
}

const centralOffset = offset
for (const record of central) write(record)
const centralSize = offset - centralOffset
write(
  Buffer.concat([
    u32(END_OF_CENTRAL),
    u16(0),
    u16(0),
    u16(central.length),
    u16(central.length),
    u32(centralSize),
    u32(centralOffset),
    u16(0),
  ]),
)
await new Promise((done, fail) => out.end((error) => (error ? fail(error) : done())))
console.log(`Wrote ${target} (${files.length} files)`)
