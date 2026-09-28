const branch = process.argv[2];

if (!branch) {
  console.error("Usage: node scripts/validate-branch-name.mjs <branch>");
  process.exit(2);
}

const allowed = /^(features|bugs|releases|hotfixes|tags)\/[a-z0-9][a-z0-9._-]*$/;

if (!allowed.test(branch)) {
  console.error(
    `Invalid working branch "${branch}". Expected features/*, bugs/*, releases/*, hotfixes/*, or tags/*.`,
  );
  process.exit(1);
}

console.log(`Working branch accepted: ${branch}`);
