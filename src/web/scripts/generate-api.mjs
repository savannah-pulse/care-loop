import { writeFile, mkdir } from 'node:fs/promises'
import openapiTS, { astToString } from 'openapi-typescript'

const output = new URL('../src/api/schema.d.ts', import.meta.url)
const schema = await openapiTS(new URL('../../../contracts/openapi/careloop.json', import.meta.url))
await mkdir(new URL('../src/api/', import.meta.url), { recursive: true })
await writeFile(output, astToString(schema))
