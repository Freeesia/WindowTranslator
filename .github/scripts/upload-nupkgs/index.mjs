import artifact from '@actions/artifact';
import { readdir } from 'node:fs/promises';
import path from 'node:path';

const directory = path.resolve(process.env.INPUT_PATH ?? 'pack');
const packages = (await readdir(directory, { withFileTypes: true }))
  .filter(entry => entry.isFile() && entry.name.endsWith('.nupkg'))
  .map(entry => entry.name)
  .sort((left, right) => left.localeCompare(right));

if (packages.length === 0) {
  throw new Error(`No NuGet packages found in ${directory}.`);
}

for (const packageName of packages) {
  const packagePath = path.join(directory, packageName);
  console.log(`Uploading ${packageName}`);
  await artifact.uploadArtifact(packageName, [packagePath], directory, { skipArchive: true });
}
