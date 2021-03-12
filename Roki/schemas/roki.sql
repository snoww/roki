create table users
(
    id            bigint primary key,
    username      text not null,
    discriminator text not null,
    avatar        text
);

create table guild
(
    id         bigint primary key,
    name       text   not null,
    icon       text,
    owner_id   bigint not null references users (id),
    moderators bigint[],
    available  bool
);

create table channel
(
    id           bigint primary key,
    guild_id     bigint not null references guild (id),
    name         text   not null,
    deleted_date date
);

create table guild_config
(
    guild_id              bigint primary key references guild (id),
    prefix                text             not null,
    logging               bool             not null,
    currency              bool             not null,
    xp                    bool             not null,
    currency_default      bigint           not null,
    investing_default     bigint           not null,
    currency_chance       double precision not null,
    currency_cd           int              not null,
    currency_icon         text             not null,
    currency_name         text             not null,
    currency_plural       text             not null,
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

create table store_item
(
    id          serial primary key,
    guild_id    bigint not null references guild (id),
    seller_id   bigint not null references users (id),
    name        text   not null,
    description text,
    details     text unique,
    category    text,
    duration    int,
    price       int,
    quantity    int
);

create table xp_reward
(
    id          serial primary key,
    guild_id    bigint not null references guild (id),
    level       int    not null,
    details     text   not null unique,
    description text
);

create table user_data
(
    uid                   bigint    not null references users (id),
    guild_id              bigint    not null references guild (id),
    xp                    bigint    not null,
    last_level_up         timestamp not null,
    last_xp_gain          timestamp not null,
    notification_location int       not null,
    currency              bigint    not null,
    investing             decimal   not null,
    primary key (uid, guild_id)
);

create table inventory_item
(
    uid      bigint not null references users (id),
    guild_id bigint not null references guild (id),
    item_id  int references store_item (id),
    quantity int    not null,
    primary key (uid, guild_id, item_id)
);

create table subscription
(
    uid      bigint not null references users (id),
    guild_id bigint not null references guild (id),
    item_id  int references store_item (id),
    expiry   date   not null,
    primary key (uid, guild_id, item_id)
);

create table investment
(
    id            serial primary key,
    uid           bigint not null references users (id),
    guild_id      bigint not null references guild (id),
    symbol        text   not null,
    shares        bigint not null,
    interest_date date
);

create table trade
(
    id            serial primary key,
    investment_id int       not null references investment (id),
    guild_id      bigint    not null references guild (id),
    uid           bigint    not null references users (id),
    symbol        text      not null,
    shares        bigint    not null,
    price         decimal   not null,
    date          timestamp not null
);

create table message
(
    id          bigint primary key,
    channel_id  bigint not null,
    guild_id    bigint not null,
    author_id   bigint not null,
    content     text,
    replied_to  bigint,
    edits       jsonb,
    attachments text[],
    deleted     bool   not null
);

create table event
(
    id           serial primary key,
    guild_id     bigint    not null references guild (id),
    name         text      not null,
    description  text,
    host_id      bigint    not null references users (id),
    start_date   timestamp not null,
    channel_id   bigint    not null references channel (id),
    message_id   bigint    not null,
    participants text[],
    undecided    text[]
);

create table quote
(
    id        serial primary key,
    guild_id  bigint    not null references guild (id),
    author_id bigint    not null references users (id),
    keyword   text      not null,
    text      text      not null,
    context   text,
    use_count int       not null,
    date      timestamp not null
);

create table transaction
(
    id          serial primary key,
    guild_id    bigint not null references guild (id),
    sender      bigint not null references users (id),
    recipient   bigint not null references users (id),
    amount      bigint not null,
    description text,
    channel_id  bigint not null references channel (id),
    message_id  bigint not null references message (id)
);

-- todo pokemon
-- todo jeopardy