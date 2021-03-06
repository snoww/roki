create table users
(
    id            bigint primary key,
    username      varchar(32) not null,
    discriminator varchar(4)  not null,
    avatar        varchar(50) not null
);

create table guild
(
    id         bigint primary key,
    name       varchar(100) not null,
    icon       varchar(50),
    owner_id   bigint       not null references users (id),
    moderators bigint[]
);

create table channel
(
    id           bigint primary key,
    guild_id     bigint references guild (id),
    name         varchar(100) not null,
    deleted_date date
);

create table guild_config
(
    guild_id              bigint primary key references guild (id),
    logging               bool             not null,
    currency              bool             not null,
    xp                    bool             not null,
    currency_chance       double precision not null,
    currency_cd           int              not null,
    currency_icon         varchar(50)      not null,
    currency_name         varchar(20)      not null,
    currency_plural       varchar(20)      not null,
    currency_drop         int              not null,
    currency_drop_max     int              not null,
    currency_drop_rare    int              not null,
    xp_per_message        int              not null,
    xp_cd                 int              not null,
    xp_fast_cd            int              not null,
    bf_min                int              not null,
    bf_multiplier         double precision not null,
    bfm_min               int              not null,
    bfm_min_guess         int              not null,
    bfm_min_correct       double precision not null,
    bfm_multiplier        double precision not null,
    bd_min                int              not null,
    br_min                int              not null,
    br_71                 double precision not null,
    br_92                 double precision not null,
    br_100                double precision not null,
    trivia_min_correct    double precision not null,
    trivia_easy           int              not null,
    trivia_med            int              not null,
    trivia_hard           int              not null,
    notification_location int              not null
);

create table channel_config
(
    channel_id bigint primary key references channel (id),
    logging    bool not null,
    currency   bool not null,
    xp         bool not null
);

create table store
(
    id          serial primary key,
    guild_id    bigint      not null references guild (id),
    seller_id   bigint      not null references users (id),
    name        varchar(50) not null,
    description text,
    details     varchar(100),
    category    varchar(50),
    duration    int,
    price       int         not null,
    quantity    int         not null
);

create table xp_reward
(
    id       serial primary key,
    guild_id bigint not null references guild (id),
    level    int    not null,
    type     varchar(20),
    reward   varchar(50)
);

create table user_data
(
    uid                   bigint                   not null references users (id),
    guild_id              bigint                   not null references guild (id),
    xp                    int                      not null,
    last_level_up         timestamp with time zone not null,
    notification_location int                      not null,
    currency              bigint                   not null,
    investing             decimal                  not null,
    primary key (uid, guild_id)
);

create table inventory
(
    uid      bigint not null references users (id),
    guild_id bigint not null references guild (id),
    item_id  int references store (id),
    quantity int    not null,
    primary key (uid, guild_id, item_id)
);

create table subscription
(
    uid      bigint not null references users (id),
    guild_id bigint not null references guild (id),
    item_id  int references store (id),
    expiry   date,
    primary key (uid, guild_id, item_id)
);

create table investment
(
    uid           bigint      not null references users (id),
    guild_id      bigint      not null references guild (id),
    symbol        varchar(10) not null,
    shares        bigint      not null,
    interest_date date,
    primary key (uid, guild_id, symbol)
);


