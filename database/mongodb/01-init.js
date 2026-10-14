const serviceMode = process.env.MONGO_APP_DATABASE && process.env.MONGO_APP_USER && process.env.MONGO_APP_PASSWORD
  ? [
      {
        database: process.env.MONGO_APP_DATABASE,
        user: process.env.MONGO_APP_USER,
        password: process.env.MONGO_APP_PASSWORD,
      },
    ]
  : [];

const legacyMode = [
  {
    database: 'identity_logs',
    user: process.env.MONGO_IDENTITY_USER,
    password: process.env.MONGO_IDENTITY_PASSWORD,
  },
  {
    database: 'collaby_messenger',
    user: process.env.MONGO_MESSENGER_USER,
    password: process.env.MONGO_MESSENGER_PASSWORD,
  },
  {
    database: 'collaby_calls',
    user: process.env.MONGO_CALLS_USER,
    password: process.env.MONGO_CALLS_PASSWORD,
  },
  {
    database: 'collaby_gamification',
    user: process.env.MONGO_GAMIFICATION_USER,
    password: process.env.MONGO_GAMIFICATION_PASSWORD,
  },
].filter((service) => service.user || service.password);

const services = serviceMode.length > 0 ? serviceMode : legacyMode;

if (services.length === 0) {
  print('No Mongo application bootstrap variables detected. Skipping Mongo user bootstrap.');
  quit(0);
}

for (const service of services) {
  if (!service.user || !service.password) {
    throw new Error(`Mongo credentials for database '${service.database}' must be set.`);
  }

  const serviceDb = db.getSiblingDB(service.database);
  const existingUser = serviceDb.getUser(service.user);

  if (existingUser) {
    serviceDb.updateUser(service.user, {
      pwd: service.password,
      roles: [{ role: 'readWrite', db: service.database }],
    });
    continue;
  }

  serviceDb.createUser({
    user: service.user,
    pwd: service.password,
    roles: [{ role: 'readWrite', db: service.database }],
  });
}
