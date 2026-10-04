function requireInteger(name, value, minimum, maximum) {
  if (!Number.isInteger(value) || value < minimum || value > maximum) {
    throw new RangeError(`${name} must be an integer between ${minimum} and ${maximum}.`);
  }
}

function parseReleaseDate(value) {
  if (!/^\d{8}$/.test(value)) {
    throw new RangeError("Release date must use YYYYMMDD.");
  }

  const year = Number(value.slice(0, 4));
  const month = Number(value.slice(4, 6));
  const day = Number(value.slice(6, 8));
  const instant = new Date(Date.UTC(year, month - 1, day));

  if (
    instant.getUTCFullYear() !== year ||
    instant.getUTCMonth() !== month - 1 ||
    instant.getUTCDate() !== day
  ) {
    throw new RangeError("Release date must be a valid calendar date.");
  }

  const startOfYear = Date.UTC(year, 0, 1);
  const dayOfYear = Math.floor((instant.getTime() - startOfYear) / 86_400_000) + 1;
  const fileDate = (year % 100) * 1000 + dayOfYear;

  if (fileDate > 65535) {
    throw new RangeError("Encoded file-version date exceeds 65535.");
  }

  return { year, dayOfYear, fileDate };
}

export function computeReleaseVersion({
  major,
  minor,
  date,
  build,
  sha,
}) {
  requireInteger("major", major, 0, 65_535);
  requireInteger("minor", minor, 0, 65_535);
  requireInteger("build", build, 1, 65_535);

  const sourceSha = String(sha ?? "").trim();
  if (!/^[0-9a-f]{6,40}$/i.test(sourceSha)) {
    throw new RangeError("A hexadecimal source SHA (6-40 characters) is required.");
  }

  const { fileDate } = parseReleaseDate(date);
  const packageVersion = `${major}.${minor}.${date}.${build}`;

  return Object.freeze({
    packageVersion,
    assemblyVersion: `${major}.${minor}.0.0`,
    fileVersion: `${major}.${minor}.${fileDate}.${build}`,
    informationalVersion: `${packageVersion}+${sourceSha.toLowerCase()}`,
  });
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [major, minor, date, build, sha] = process.argv.slice(2);
  const version = computeReleaseVersion({
    major: Number(major),
    minor: Number(minor),
    date,
    build: Number(build),
    sha,
  });
  process.stdout.write(`${JSON.stringify(version, null, 2)}\n`);
}
